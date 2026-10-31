using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using FIAP.TechChallenge.Execution.Infrastructure.Messaging;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class ExecutionHost : IAsyncDisposable
{
    public const string Root = "/api/execution/v1";
    public const string Key = "integration-tests-signing-key-at-least-32-bytes";
    public const string KafkaImage = "apache/kafka:4.4.0@sha256:833b867a9585a141da455dc2b57431c65cbccb27f11c91f694444cca9f777e0f";
    public const string MongoImage = "mongo:8.0@sha256:b9924c330a8bbc5800fe9c69f04a84e221eca7b28fd049a0354147d9908fa142";
    public readonly INetwork Network = new NetworkBuilder().Build();
    public MongoDbContainer Mongo { get; private set; } = null!;
    public IContainer Kafka { get; private set; } = null!;
    private WebApplicationFactory<API.Program> _factory = null!;
    private IProducer<string, string> _producer = null!;
    private IContainer? _image;
    private IMongoDatabase _database = null!;
    public HttpClient Client { get; private set; } = null!;
    public string BootstrapServers { get; private set; } = "";
    public string DatabaseName { get; } = "test_" + Guid.NewGuid().ToString("N");
    public IMongoDatabase Database => _database;
    public IServiceProvider Services => _factory.Services;

    public async Task StartAsync(bool useImage = false)
    {
        await Network.CreateAsync();
        Mongo = new MongoDbBuilder(MongoImage).WithReplicaSet("rs1")
            .WithNetwork(Network).WithNetworkAliases("mongo").Build();
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        BootstrapServers = $"localhost:{port}";
        Kafka = new ContainerBuilder(KafkaImage).WithNetwork(Network).WithNetworkAliases("kafka").WithPortBinding(port, 9092)
            .WithEnvironment("CLUSTER_ID", "MkU3OEVBNTcwNTJENDM2Qk")
            .WithEnvironment("KAFKA_NODE_ID", "1").WithEnvironment("KAFKA_PROCESS_ROLES", "broker,controller")
            .WithEnvironment("KAFKA_LISTENERS", "PLAINTEXT://:19092,EXTERNAL://:9092,CONTROLLER://:19093")
            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://kafka:19092,EXTERNAL://localhost:{port}")
            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,EXTERNAL:PLAINTEXT,CONTROLLER:PLAINTEXT")
            .WithEnvironment("KAFKA_INTER_BROKER_LISTENER_NAME", "PLAINTEXT").WithEnvironment("KAFKA_CONTROLLER_LISTENER_NAMES", "CONTROLLER")
            .WithEnvironment("KAFKA_CONTROLLER_QUORUM_VOTERS", "1@localhost:19093")
            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1").WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
            .WithEnvironment("KAFKA_GROUP_INITIAL_REBALANCE_DELAY_MS", "0")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Kafka Server started"))
            .Build();
        await Task.WhenAll(Mongo.StartAsync(), Kafka.StartAsync());
        MongoDB.Bson.Serialization.BsonSerializer.TryRegisterSerializer(new MongoDB.Bson.Serialization.Serializers.GuidSerializer(MongoDB.Bson.GuidRepresentation.Standard));
        _database = new MongoClient(Mongo.GetConnectionString()).GetDatabase(DatabaseName);
        if (useImage)
        {
            var image = Environment.GetEnvironmentVariable("EXECUTION_TEST_IMAGE") ?? throw new InvalidOperationException("EXECUTION_TEST_IMAGE deve apontar para a imagem construida.");
            var mongo = new MongoUrlBuilder(Mongo.GetConnectionString()) { Server = new MongoServerAddress("mongo", 27017), DirectConnection = true };
            _image = new ContainerBuilder(image).WithNetwork(Network).WithPortBinding(8080, true)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing").WithEnvironment("ASPNETCORE_HTTP_PORTS", "8080")
                .WithEnvironment("ConnectionStrings__MongoDB", mongo.ToString()).WithEnvironment("MongoDB__Database", DatabaseName)
                .WithEnvironment("Messaging__BootstrapServers", "kafka:19092").WithEnvironment("Messaging__Enabled", "true")
                .WithEnvironment("Jwt__Issuer", "test-issuer").WithEnvironment("Jwt__Audience", "test-audience").WithEnvironment("Jwt__SigningKey", Key)
                .WithEnvironment("Logging__LogLevel__Default", "Warning")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath(Root + "/health/ready")))
                .Build();
            await _image.StartAsync();
            Client = new HttpClient { BaseAddress = new Uri($"http://{_image.Hostname}:{_image.GetMappedPublicPort(8080)}") };
        }
        else
        {
            _factory = new Factory(Mongo.GetConnectionString(), DatabaseName, BootstrapServers);
            Client = _factory.CreateClient();
        }
        Client.Timeout = TimeSpan.FromSeconds(30);
        Authenticate();
        _producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = BootstrapServers, EnableIdempotence = true }).Build();
        await WaitAsync(async () => (await Client.GetAsync(Root + "/health/ready")).IsSuccessStatusCode);
    }
    public void Authenticate(bool expired = false) => Client.DefaultRequestHeaders.Authorization = new("Bearer", Token(expired));
    public async Task RestartApiAsync()
    {
        Client.Dispose();
        if (_image is not null)
        {
            await _image.StopAsync(); await _image.StartAsync();
            Client = new HttpClient { BaseAddress = new Uri($"http://{_image.Hostname}:{_image.GetMappedPublicPort(8080)}") };
        }
        else
        {
            await _factory.DisposeAsync(); _factory = new Factory(Mongo.GetConnectionString(), DatabaseName, BootstrapServers); Client = _factory.CreateClient();
        }
        Authenticate();
    }
    public static string Token(bool expired = false) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "test-issuer", Audience = "test-audience", Subject = new ClaimsIdentity([new Claim("sub", "operator")]),
        NotBefore = DateTime.UtcNow.AddHours(-2), Expires = expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256)
    });
    public async Task<Guid> CreateOrderAsync()
    {
        var id = Guid.NewGuid();
        await SendAsync(Envelope("OrdemCriada", new FlowPayload(id, 1, Guid.NewGuid(), Guid.NewGuid(), "Motor com falha")));
        await WaitAsync(async () => (await Client.GetAsync(Root + "/ordens/" + id)).IsSuccessStatusCode);
        return id;
    }
    public static MessageEnvelope<FlowPayload> Envelope(string type, FlowPayload payload, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), type, 1, DateTimeOffset.UtcNow, "integration-correlation", null, null, null, payload);
    public Task SendAsync(MessageEnvelope<FlowPayload> message) => SendRawAsync("fiap.os.events.v1", message.Payload.OrdemServicoId.ToString(), IntegrationContext.Serialize(message));
    public async Task SendRawAsync(string topic, string key, string body) =>
        await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = body });
    public async Task<JsonElement> CreateServicoAsync(string descricao = "Troca de filtros", decimal valor = 35.50m) =>
        await PostCreatedAsync("/servicos", new { Descricao = descricao, ValorUnitario = valor });
    public async Task<JsonElement> CreatePecaAsync(int estoque = 10, decimal preco = 12.25m) =>
        await PostCreatedAsync("/pecas-insumos", new { Nome = "Filtro original", Codigo = "p-" + Guid.NewGuid().ToString("N"), PrecoUnitario = preco, QuantidadeEstoque = estoque });
    public async Task<JsonElement> PostCreatedAsync(string path, object body, string? key = null)
    {
        using var response = await RequestAsync(HttpMethod.Post, path, body, key);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() => { Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), text); Assert.That(response.Headers.Location, Is.Not.Null); });
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    public Task<HttpResponseMessage> RequestAsync(HttpMethod method, string path, object? body = null, string? key = null)
    {
        var request = new HttpRequestMessage(method, Root + path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return Client.SendAsync(request);
    }
    public async Task<JsonElement> GetAsync(string path) => JsonDocument.Parse(await Client.GetStringAsync(Root + path)).RootElement.Clone();
    public async Task WaitStateAsync(Guid id, int state) => await WaitAsync(async () =>
    {
        using var response = await Client.GetAsync(Root + "/ordens/" + id);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("estado").GetInt32() == state;
    });
    public static async Task WaitAsync(Func<Task<bool>> condition, int seconds = 30)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until) { if (await condition()) return; await Task.Delay(100); }
        Assert.Fail("A condicao nao foi atingida antes do timeout.");
    }
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose(); _producer?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        if (_image is not null) await _image.DisposeAsync();
        if (Kafka is not null) await Kafka.DisposeAsync();
        if (Mongo is not null) await Mongo.DisposeAsync();
        await Network.DisposeAsync();
    }
    private sealed class Factory(string connection, string database, string kafka) : WebApplicationFactory<API.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MongoDB"] = connection, ["MongoDB:Database"] = database,
                ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience", ["Jwt:SigningKey"] = Key,
                ["Messaging:Enabled"] = "true", ["Messaging:BootstrapServers"] = kafka,
                ["Messaging:RetryDelaysSeconds:0"] = "0", ["Messaging:RetryDelaysSeconds:1"] = "0", ["Messaging:RetryDelaysSeconds:2"] = "0",
                ["Messaging:RetryDelaysSeconds:3"] = "0", ["Messaging:RetryDelaysSeconds:4"] = "0",
                ["Logging:LogLevel:Default"] = "Warning", ["Observability:EnableOtlp"] = "false"
            }));
        }
    }
}
