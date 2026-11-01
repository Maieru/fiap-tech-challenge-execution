using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FIAP.TechChallenge.Execution.API;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.OS.Application.Messaging;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Testcontainers.Kafka;
using Testcontainers.MongoDb;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using DotNet.Testcontainers.Containers;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

[SetUpFixture]
public sealed class TestEnvironment
{
    private string? previousSigningKey;
    public static readonly INetwork Network = new NetworkBuilder().WithName($"execution-tests-{Guid.NewGuid():N}").Build();
    public static readonly MongoDbContainer Mongo = new MongoDbBuilder("mongo:8.0")
        .WithNetwork(Network).WithNetworkAliases("mongo").WithReplicaSet("execution-tests").Build();
    public static readonly KafkaContainer Kafka = new KafkaBuilder("confluentinc/cp-kafka:7.9.2").WithKRaft()
        .WithNetwork(Network).WithNetworkAliases("kafka").WithListener("kafka:19092")
        .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false").Build();

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        TestContext.Progress.WriteLine($"[containers] Inicializando dependências; SO: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}; limite: 3 minutos.");
        try
        {
            previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
            Environment.SetEnvironmentVariable("Jwt__SigningKey", ExecutionFactory.SigningKey);
            await Network.CreateAsync(timeout.Token);
            await Task.WhenAll(
                StartContainerAsync("MongoDB replica set", Mongo, timeout.Token),
                StartContainerAsync("Kafka", Kafka, timeout.Token)).WaitAsync(timeout.Token);
            using var admin = Admin();
            await admin.CreateTopicsAsync([new TopicSpecification
            {
                Name = Application.Messaging.ExecutionMessaging.Topic, NumPartitions = 1, ReplicationFactor = 1
            }], new CreateTopicsOptions { OperationTimeout = TimeSpan.FromSeconds(10), RequestTimeout = TimeSpan.FromSeconds(15) });
            TestContext.Progress.WriteLine("[containers] Dependências e tópico prontos; iniciando testes HTTP.");
        }
        catch (Exception exception)
        {
            Environment.SetEnvironmentVariable("Jwt__SigningKey", previousSigningKey);
            await Task.WhenAll(PrintLogsAsync("MongoDB", Mongo), PrintLogsAsync("Kafka", Kafka));
            if (timeout.IsCancellationRequested)
                throw new TimeoutException("MongoDB/Kafka não inicializaram em 3 minutos. Consulte os logs dos containers acima.", exception);
            throw;
        }
    }
    private static async Task StartContainerAsync(string name, IContainer container, CancellationToken cancellationToken)
    {
        var elapsed = Stopwatch.StartNew();
        TestContext.Progress.WriteLine($"[containers] Iniciando {name}...");
        await container.StartAsync(cancellationToken);
        TestContext.Progress.WriteLine($"[containers] {name} pronto em {elapsed.Elapsed.TotalSeconds:F1}s.");
    }
    private static async Task PrintLogsAsync(string name, IContainer container)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var logs = await container.GetLogsAsync(ct: timeout.Token);
            TestContext.Error.WriteLine($"[containers] {name}: {container.Id}, estado {container.State}");
            TestContext.Error.WriteLine(string.Join('\n', (logs.Stdout + logs.Stderr).Split('\n').TakeLast(80)));
        }
        catch (Exception exception) { TestContext.Error.WriteLine($"[containers] Logs de {name} indisponíveis: {exception.Message}"); }
    }
    [OneTimeTearDown]
    public async Task StopAsync()
    {
        try { await Kafka.DisposeAsync(); await Mongo.DisposeAsync(); await Network.DisposeAsync(); }
        finally { Environment.SetEnvironmentVariable("Jwt__SigningKey", previousSigningKey); }
    }
    public static IAdminClient Admin() => new AdminClientBuilder(new AdminClientConfig
    { BootstrapServers = Kafka.GetBootstrapAddress(), BrokerAddressFamily = BrokerAddressFamily.V4 }).Build();
}

public sealed class ExecutionFactory(string databaseName, string inputTopic, string groupId, int timeoutMs = 10000) : WebApplicationFactory<Program>
{
    public static readonly string SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = TestEnvironment.Mongo.GetConnectionString(),
            ["Mongo:DatabaseName"] = databaseName,
            ["Kafka:BootstrapServers"] = TestEnvironment.Kafka.GetBootstrapAddress(),
            ["Kafka:InputTopic"] = inputTopic, ["Kafka:GroupId"] = groupId,
            ["Kafka:MessageTimeoutMs"] = timeoutMs.ToString(),
            ["Logging:LogLevel:Default"] = "Warning",
            ["Jwt:Issuer"] = "execution-tests", ["Jwt:Audience"] = "execution-tests"
        }));
    }
    public static string Token(bool expired = false, string issuer = "execution-tests", string? signingKey = null) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer, audience: "execution-tests", expires: DateTime.UtcNow.AddMinutes(expired ? -15 : 15),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey ?? SigningKey)), SecurityAlgorithms.HmacSha256)));
}

[NonParallelizable]
public abstract class IntegrationTest
{
    protected const string Prefix = "/api/execution/v1";
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    protected ExecutionFactory Factory = null!;
    protected HttpClient Client = null!;
    protected string DatabaseName = null!;
    protected string InputTopic = null!;
    protected string GroupId = null!;

    [SetUp]
    public async Task StartAppAsync()
    {
        DatabaseName = $"execution_{Guid.NewGuid():N}";
        InputTopic = $"os-test-{Guid.NewGuid():N}";
        GroupId = $"execution-test-{Guid.NewGuid():N}";
        using var admin = TestEnvironment.Admin();
        await admin.CreateTopicsAsync([new TopicSpecification { Name = InputTopic, NumPartitions = 1, ReplicationFactor = 1 }]);
        Factory = new ExecutionFactory(DatabaseName, InputTopic, GroupId);
        Client = Factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ExecutionFactory.Token());
    }

    [TearDown]
    public async Task StopAppAsync()
    {
        Client?.Dispose();
        if (Factory is not null) await Factory.DisposeAsync();
        if (DatabaseName is not null) await new MongoClient(TestEnvironment.Mongo.GetConnectionString()).DropDatabaseAsync(DatabaseName);
        if (InputTopic is not null)
        {
            using var admin = TestEnvironment.Admin();
            await admin.DeleteTopicsAsync([InputTopic]);
        }
    }

    protected async Task<DeliveryResult<string, string>> SendAsync<T>(Guid orderId, string type, T payload, Guid? messageId = null)
    {
        var envelope = new MessageEnvelope<T>(messageId ?? Guid.NewGuid(), type, OSMessaging.Version,
            DateTimeOffset.UtcNow, "integration-correlation", null, null, null, payload);
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = TestEnvironment.Kafka.GetBootstrapAddress(), Acks = Acks.All,
            EnableIdempotence = true, MessageTimeoutMs = 10000, BrokerAddressFamily = BrokerAddressFamily.V4
        }).Build();
        return await producer.ProduceAsync(InputTopic, new Message<string, string> { Key = orderId.ToString("D"), Value = JsonSerializer.Serialize(envelope, Json) });
    }

    protected async Task<Guid> ReceiveOrderAsync(Guid? existingId = null, Guid? messageId = null)
    {
        var id = existingId ?? Guid.NewGuid();
        await SendAsync(id, OrdemServicoCriada.Type, new OrdemServicoCriada(id,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Motor com falha"), messageId);
        await WaitOrderAsync(id, StatusExecucao.Recebida);
        return id;
    }

    protected async Task<ExecucaoResponse> WaitOrderAsync(Guid id, StatusExecucao expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            using var response = await Client.GetAsync($"{Prefix}/ordens/{id}", timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<ExecucaoResponse>(Json, timeout.Token);
                if (value?.Execucao.Status == expected) return value;
            }
            await Task.Delay(100, timeout.Token);
        }
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(expected), body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    protected async Task<Guid> ServiceAsync(decimal price = 100.25m)
    {
        using var response = await Client.PostAsJsonAsync($"{Prefix}/servicos", new { descricao = "Revisão", valorUnitario = price });
        return (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("id").GetGuid();
    }
    protected async Task<Guid> PartAsync(int stock = 10, string? code = null)
    {
        using var response = await Client.PostAsJsonAsync($"{Prefix}/pecas-insumos", new
        { nome = "Filtro", codigo = code ?? $"FL-{Guid.NewGuid():N}", precoUnitario = 20.15m, quantidadeEstoque = stock });
        return (await ReadAsync(response, HttpStatusCode.Created)).GetProperty("id").GetGuid();
    }

    protected async Task<JsonElement> PutAsync(string route, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await Client.PutAsJsonAsync($"{Prefix}/{route}", body ?? new { });
        return await ReadAsync(response, expected);
    }
    protected async Task<ExecucaoResponse> GetOrderAsync(Guid id) =>
        (await Client.GetFromJsonAsync<ExecucaoResponse>($"{Prefix}/ordens/{id}", Json))!;
    protected async Task<int> StockAsync(Guid id) =>
        (await Client.GetFromJsonAsync<JsonElement>($"{Prefix}/pecas-insumos/{id}", Json)).GetProperty("quantidadeEstoque").GetInt32();

    protected static MessageEnvelope<JsonElement> ReadEvent(Guid id, string type)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = TestEnvironment.Kafka.GetBootstrapAddress(), GroupId = $"output-test-{Guid.NewGuid():N}",
            EnableAutoCommit = false, EnableAutoOffsetStore = false, AutoOffsetReset = AutoOffsetReset.Earliest,
            BrokerAddressFamily = BrokerAddressFamily.V4
        }).Build();
        consumer.Subscribe(Application.Messaging.ExecutionMessaging.Topic);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (true)
            {
                var record = consumer.Consume(timeout.Token);
                if (record.Message.Key != id.ToString("D")) continue;
                var envelope = JsonSerializer.Deserialize<MessageEnvelope<JsonElement>>(record.Message.Value, Json)!;
                if (envelope.Type == type) return envelope;
            }
        }
        finally { consumer.Close(); }
    }
}
