using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Reqnroll;
using Testcontainers.Kafka;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;

namespace FIAP.TechChallenge.Execution.Distributed.Tests;

[Binding]
public sealed class DistributedEnvironment
{
    public const string OsTopic = "oficina.os.events.v1";
    public const string PaymentRequestTopic = "oficina.os.billing.requests.v1";
    public const string BillingTopic = "oficina.billing.events.v1";
    public const string ExecutionTopic = "oficina.execution.events.v1";
    public static DistributedEnvironment Current { get; } = new();
    private readonly INetwork network = new NetworkBuilder().Build();
    private MongoDbContainer mongo = null!;
    private KafkaContainer kafka = null!;
    private PostgreSqlContainer postgres = null!;
    private PostgreSqlContainer billingDb = null!;
    private IContainer billing = null!;
    private IContainer os = null!;
    private IContainer execution = null!;
    private readonly string key = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    private string accessToken = null!;
    public string BillingGroup { get; } = $"billing-bdd-{Guid.NewGuid():N}";
    public string OsGroup { get; } = $"os-bdd-{Guid.NewGuid():N}";
    public string ExecutionGroup { get; } = $"execution-bdd-{Guid.NewGuid():N}";
    public string BootstrapServers => kafka.GetBootstrapAddress();

    [BeforeTestRun]
    public static Task BeforeRun() => Current.StartAsync();
    [AfterTestRun]
    public static Task AfterRun() => Current.DisposeAsync();

    private async Task StartAsync()
    {
        var billingImage = Environment.GetEnvironmentVariable("BILLING_TEST_IMAGE") ?? throw new InvalidOperationException("Configure BILLING_TEST_IMAGE.");
        var osImage = Environment.GetEnvironmentVariable("OS_TEST_IMAGE");
        var executionImage = Environment.GetEnvironmentVariable("EXECUTION_TEST_IMAGE");
        if (string.IsNullOrWhiteSpace(osImage) || string.IsNullOrWhiteSpace(executionImage))
            throw new InvalidOperationException("Build both images and configure OS_TEST_IMAGE and EXECUTION_TEST_IMAGE.");
        try
        {
            await network.CreateAsync();
            postgres = new PostgreSqlBuilder("postgres:18.3").WithNetwork(network).WithNetworkAliases("postgres")
                .WithDatabase("fiap_os").WithUsername("fiap_os").WithPassword(Guid.NewGuid().ToString("N")).Build();
            mongo = new MongoDbBuilder("mongo:8.0").WithNetwork(network).WithNetworkAliases("mongo").WithReplicaSet("distributed-tests").Build();
            kafka = new KafkaBuilder("confluentinc/cp-kafka:7.9.2").WithKRaft()
                .WithNetwork(network).WithNetworkAliases("kafka").WithListener("kafka:19092")
                .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false").Build();
            billingDb = new PostgreSqlBuilder("postgres:18.3").WithNetwork(network).WithNetworkAliases("billing-db").Build();
            await billingDb.StartAsync();
            await Task.WhenAll(postgres.StartAsync(), mongo.StartAsync(), kafka.StartAsync());
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
            await admin.CreateTopicsAsync([
                new TopicSpecification { Name = "oficina.os.billing.requests.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = "oficina.billing.events.v1", NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = OsTopic, NumPartitions = 1, ReplicationFactor = 1 },
                new TopicSpecification { Name = ExecutionTopic, NumPartitions = 1, ReplicationFactor = 1 }
            ]);
            var mongoUrl = new UriBuilder(mongo.GetConnectionString()) { Host = "mongo", Port = 27017 };
            mongoUrl.Query = string.Join("&", mongoUrl.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => !x.StartsWith("directConnection=", StringComparison.OrdinalIgnoreCase)).Append("directConnection=true"));
            var pg = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = postgres.GetConnectionString() };
            pg["Host"] = "postgres";
            pg["Port"] = 5432;
            os = Api(osImage!, "/api/os/v1/health/ready")
                .WithEnvironment("ConnectionStrings__DefaultConnection", pg.ToString())
                .WithEnvironment("Database__ApplyMigrations", "true")
                .WithEnvironment("Kafka__GroupId", OsGroup).Build();
            execution = Api(executionImage!, "/api/execution/v1/health/ready")
                .WithEnvironment("Mongo__ConnectionString", mongoUrl.ToString())
                .WithEnvironment("Mongo__DatabaseName", "fiap_execution")
                .WithEnvironment("Kafka__GroupId", ExecutionGroup).Build();
            var billingConnection = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = billingDb.GetConnectionString() }; billingConnection["Host"] = "billing-db"; billingConnection["Port"] = 5432;
            billing = Api(billingImage, "/api/billing/v1/health/ready").WithEnvironment("Kafka__GroupId", BillingGroup).WithEnvironment("ConnectionStrings__DefaultConnection", billingConnection.ToString()).WithEnvironment("Database__ApplyMigrations", "true").Build();
            await billing.StartAsync();
            await Task.WhenAll(os.StartAsync(), execution.StartAsync());
            // Obtain the administrative JWT from the real OS login, never mint one in this driver.
            using var identityClient = new HttpClient { BaseAddress = new Uri($"http://{os.Hostname}:{os.GetMappedPublicPort(8080)}") };
            var credentials = new { usuario = "bdd-" + Guid.NewGuid().ToString("N"), senha = Guid.NewGuid().ToString("N") };
            using var registered = await identityClient.PostAsJsonAsync("/api/os/v1/usuarios", credentials);
            registered.EnsureSuccessStatusCode();
            using var loggedIn = await identityClient.PostAsJsonAsync("/api/os/v1/usuarios/login", credentials);
            loggedIn.EnsureSuccessStatusCode();
            var login = await loggedIn.Content.ReadFromJsonAsync<JsonElement>();
            accessToken = login.GetProperty("token").GetString() ?? throw new InvalidOperationException("Login OS não emitiu token.");
        }
        catch { await DisposeAsync(); throw; }
    }

    private ContainerBuilder Api(string image, string readiness) => new ContainerBuilder(image)
        .WithNetwork(network).WithPortBinding(8080, true)
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing")
        .WithEnvironment("ASPNETCORE_HTTP_PORTS", "8080")
        .WithEnvironment("Kafka__BootstrapServers", "kafka:19092")
        .WithEnvironment("Jwt__Issuer", "distributed-tests")
        .WithEnvironment("Jwt__Audience", "distributed-tests")
        .WithEnvironment("Jwt__SigningKey", key)
        .WithEnvironment("Logging__LogLevel__Default", "Warning")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
            request.ForPort(8080).ForPath(readiness).ForStatusCode(HttpStatusCode.OK)));

    public HttpClient OsClient(string correlation) => Client(os, correlation);
    public HttpClient BillingClient(string correlation) => Client(billing, correlation);
    public HttpClient ExecutionClient(string correlation) => Client(execution, correlation);
    private HttpClient Client(IContainer container, string correlation)
    {
        var client = new HttpClient { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(8080)}") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Add("X-Correlation-ID", correlation);
        return client;
    }

    public async Task ReplayAsync(string topic, string type, Guid orderId, string consumerGroup)
    {
        var message = Messages(topic, orderId).First(x => x.Type == type);
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        { BootstrapServers = BootstrapServers, EnableIdempotence = true, Acks = Acks.All }).Build();
        var sent = await producer.ProduceAsync(topic, new Message<string, string> { Key = orderId.ToString("D"), Value = message.Json });
        using var probe = new ConsumerBuilder<string, string>(new ConsumerConfig
        { BootstrapServers = BootstrapServers, GroupId = consumerGroup, EnableAutoCommit = false }).Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (probe.Committed([new TopicPartition(topic, 0)], TimeSpan.FromSeconds(5)).Single().Offset.Value <= sent.Offset.Value)
            await Task.Delay(100, timeout.Token);
    }

    public IReadOnlyList<WireMessage> Messages(string topic, Guid orderId)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        { BootstrapServers = BootstrapServers, GroupId = $"inspect-{Guid.NewGuid():N}", EnableAutoCommit = false }).Build();
        var partition = new TopicPartition(topic, 0);
        var high = consumer.QueryWatermarkOffsets(partition, TimeSpan.FromSeconds(10)).High.Value;
        consumer.Assign(new TopicPartitionOffset(partition, Offset.Beginning));
        var result = new List<WireMessage>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            while (high > 0)
            {
                var received = consumer.Consume(timeout.Token);
                if (received.Message.Key == orderId.ToString("D"))
                {
                    using var json = JsonDocument.Parse(received.Message.Value);
                    result.Add(new WireMessage(json.RootElement.GetProperty("type").GetString()!, received.Message.Value,
                        json.RootElement.Clone()));
                }
                if (received.Offset.Value >= high - 1) break;
            }
        }
        finally { consumer.Close(); }
        return result;
    }

    public async Task RejectPaymentAsync(Guid orderId)
    {
        // Contract-level rejected Billing responder, isolated to this test group while the real Billing is stopped.
        using var responder = new ConsumerBuilder<string,string>(new ConsumerConfig { BootstrapServers = BootstrapServers, GroupId = BillingGroup, EnableAutoCommit = false, AutoOffsetReset = AutoOffsetReset.Earliest }).Build();
        responder.Subscribe("oficina.os.billing.requests.v1");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        ConsumeResult<string,string> record;
        do { record = responder.Consume(timeout.Token); } while (record.Message.Key != orderId.ToString("D"));
        using var document = JsonDocument.Parse(record.Message.Value);
        var request = document.RootElement;
        var p = request.GetProperty("payload");
        var envelope = new {
            messageId = Guid.NewGuid(), type = "billing.pagamento-recusado", version = 1,
            occurredAtUtc = DateTimeOffset.UtcNow, correlationId = request.GetProperty("correlationId").GetString(),
            causationId = request.GetProperty("messageId").GetGuid(), traceParent = request.GetProperty("traceParent").GetString(), traceState = (string?)null,
            payload = new { ordemServicoId = orderId, orcamentoId = p.GetProperty("orcamentoId").GetGuid(), cobrancaId = Guid.NewGuid(), valor = p.GetProperty("valor").GetDecimal(), moeda = "BRL", motivo = "recusa de teste do contrato" }
        };
        using var producer = new ProducerBuilder<string,string>(new ProducerConfig { BootstrapServers = BootstrapServers, Acks = Acks.All }).Build();
        await producer.ProduceAsync("oficina.billing.events.v1", new Message<string,string> { Key = orderId.ToString("D"), Value = JsonSerializer.Serialize(envelope) });
        responder.Commit(record); responder.Close();
    }
    public Task StopBillingAsync() => billing.StopAsync();
    public Task StartBillingAsync() => billing.StartAsync();
    public Task StopExecutionAsync() => execution.StopAsync();
    public Task StartExecutionAsync() => execution.StartAsync();

    private async Task DisposeAsync()
    {
        if (billing is not null) await billing.DisposeAsync();
        if (billingDb is not null) await billingDb.DisposeAsync();
        if (os is not null) await os.DisposeAsync();
        if (execution is not null) await execution.DisposeAsync();
        if (kafka is not null) await kafka.DisposeAsync();
        if (mongo is not null) await mongo.DisposeAsync();
        if (postgres is not null) await postgres.DisposeAsync();
        await network.DisposeAsync();
    }
}
