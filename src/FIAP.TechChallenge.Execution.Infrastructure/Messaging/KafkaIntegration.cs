using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FIAP.TechChallenge.Execution.Infrastructure.Messaging;

public sealed record FlowPayload(Guid OrdemServicoId, long Sequence, Guid? ClienteId = null, Guid? VeiculoId = null,
    string? DescricaoProblema = null, Guid? SolicitacaoId = null, string? OperadorId = null, string? Origem = null,
    JsonElement? Snapshot = null, string? Motivo = null);
public sealed record PendingMessage(Guid Id, Guid OrdemServicoId, long Sequence, string Body);
public sealed class KafkaOptions
{
    public bool Enabled { get; set; }
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string InputTopic { get; set; } = "";
    public string OutputTopic { get; set; } = "";
    public string GroupId { get; set; } = "";
    public int[] RetryDelaysSeconds { get; set; } = [1, 2, 4, 8, 16];
}
public interface IOutboxStore
{
    Task<bool> AcquireLeaseAsync(string owner, CancellationToken ct);
    Task<PendingMessage?> NextAsync(CancellationToken ct);
    Task MarkPublishedAsync(Guid id, CancellationToken ct);
    Task<long> PendingCountAsync(CancellationToken ct);
}
public interface IIntegrationEventHandler
{
    Task ApplyAsync(MessageEnvelope<FlowPayload> message, CancellationToken ct);
}
public sealed class IntegrationContext
{
    public MessageEnvelope<FlowPayload>? Incoming { get; set; }
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public MessageEnvelope<FlowPayload> Create(string type, FlowPayload payload) => new(Guid.NewGuid(), type, 1, DateTimeOffset.UtcNow,
        Incoming?.CorrelationId ?? Activity.Current?.GetBaggageItem("correlation.id") ?? Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
        Incoming?.MessageId, Activity.Current?.Id, Activity.Current?.TraceStateString, payload);
    public static string Serialize(MessageEnvelope<FlowPayload> message) => JsonSerializer.Serialize(message, Json);
}

public sealed class KafkaWorker(IServiceScopeFactory scopes, KafkaOptions options, ILogger<KafkaWorker> logger) : BackgroundService
{
    public const string InstrumentationName = "Fiap.TechChallenge.Messaging";
    private static readonly ActivitySource Traces = new(InstrumentationName);
    private readonly Meter _meter = new(InstrumentationName);
    private readonly string _owner = Guid.NewGuid().ToString("N");
    private long _pending;
    private Counter<long> _retries = null!;
    private Counter<long> _deadLetters = null!;
    private Histogram<long> _consumerLag = null!;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled) return;
        _retries = _meter.CreateCounter<long>("messaging.retries");
        _deadLetters = _meter.CreateCounter<long>("messaging.dead_letters");
        _consumerLag = _meter.CreateHistogram<long>("messaging.consumer_lag", "messages");
        _meter.CreateObservableGauge("messaging.outbox.pending", () => Interlocked.Read(ref _pending));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await EnsureTopicsAsync(stoppingToken); break; }
            catch (Exception error) when (error is not OperationCanceledException)
            { logger.LogWarning(error, "Kafka indisponivel durante a inicializacao; as mensagens permanecem na Outbox."); await Task.Delay(1000, stoppingToken); }
        }
        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        { BootstrapServers = options.BootstrapServers, EnableIdempotence = true, Acks = Acks.All, MessageTimeoutMs = 5000 }).Build();
        await Task.WhenAll(PublishAsync(producer, stoppingToken), Task.Run(() => ConsumeAsync(producer, stoppingToken), stoppingToken));
    }

    private async Task EnsureTopicsAsync(CancellationToken ct)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = options.BootstrapServers, SocketTimeoutMs = 5000 }).Build();
        var topics = new[] { "fiap.os.events.v1", "fiap.execution.events.v1", "fiap.os.events.v1.dlq", "fiap.execution.events.v1.dlq" };
        try
        {
            await admin.CreateTopicsAsync(topics.Select(name => new TopicSpecification { Name = name, NumPartitions = 3, ReplicationFactor = 1 }),
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(5), OperationTimeout = TimeSpan.FromSeconds(5) }).WaitAsync(ct);
        }
        catch (CreateTopicsException error) when (error.Results.All(x => !x.Error.IsError || x.Error.Code == Confluent.Kafka.ErrorCode.TopicAlreadyExists)) { }
    }

    private async Task PublishAsync(IProducer<string, string> producer, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                Interlocked.Exchange(ref _pending, await store.PendingCountAsync(ct));
                if (await store.AcquireLeaseAsync(_owner, ct) && await store.NextAsync(ct) is { } next)
                {
                    var envelope = JsonSerializer.Deserialize<MessageEnvelope<FlowPayload>>(next.Body, IntegrationContext.Json)!;
                    using var activity = StartActivity("kafka.publish", envelope);
                    await producer.ProduceAsync(options.OutputTopic, new Message<string, string>
                    { Key = next.OrdemServicoId.ToString(), Value = next.Body, Headers = TraceHeaders(envelope) }, ct);
                    await store.MarkPublishedAsync(next.Id, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning(error, "Publicacao falhou; mensagem mantida na Outbox."); }
            await Task.Delay(100, ct);
        }
    }

    private async Task ConsumeAsync(IProducer<string, string> producer, CancellationToken ct)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = options.BootstrapServers, GroupId = options.GroupId, EnableAutoCommit = false,
            EnableAutoOffsetStore = false, AutoOffsetReset = AutoOffsetReset.Earliest, MaxPollIntervalMs = 300000
        }).Build();
        consumer.Subscribe(options.InputTopic);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                ConsumeResult<string, string>? record;
                try { record = consumer.Consume(TimeSpan.FromMilliseconds(200)); }
                catch (ConsumeException error) { logger.LogWarning(error, "Falha na leitura Kafka."); await Task.Delay(200, ct); continue; }
                if (record is null) continue;
                var handled = false;
                string? failure = null;
                for (var attempt = 0; attempt <= options.RetryDelaysSeconds.Length; attempt++)
                {
                    try
                    {
                        var message = JsonSerializer.Deserialize<MessageEnvelope<FlowPayload>>(record.Message.Value, IntegrationContext.Json)
                            ?? throw new JsonException("Envelope ausente.");
                        if (message.MessageId == Guid.Empty || message.Version != 1 || message.Payload.OrdemServicoId == Guid.Empty || message.Payload.Sequence < 1
                            || record.Message.Key != message.Payload.OrdemServicoId.ToString() || string.IsNullOrWhiteSpace(message.CorrelationId))
                            throw new JsonException("Envelope, chave ou versao invalidos.");
                        using var activity = StartActivity("kafka.consume", message);
                        using var logScope = logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = message.CorrelationId, ["MessageId"] = message.MessageId });
                        await using var scope = scopes.CreateAsyncScope();
                        scope.ServiceProvider.GetRequiredService<IntegrationContext>().Incoming = message;
                        await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler>().ApplyAsync(message, ct);
                        handled = true;
                        break;
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        failure = error.GetBaseException().Message;
                        logger.LogWarning(error, "Falha no consumo da particao {Partition}, offset {Offset}, tentativa {Attempt}.", record.Partition.Value, record.Offset.Value, attempt + 1);
                        if (attempt < options.RetryDelaysSeconds.Length)
                        { _retries.Add(1); await Task.Delay(TimeSpan.FromSeconds(options.RetryDelaysSeconds[attempt]), ct); }
                    }
                }
                if (!handled)
                {
                    // Do not advance the source offset until the DLQ confirms persistence.
                    var confirmed = false;
                    while (!confirmed && !ct.IsCancellationRequested)
                    {
                        try
                        {
                            var headers = new Headers { { "original-topic", Encoding.UTF8.GetBytes(record.Topic) },
                                { "original-partition", Encoding.UTF8.GetBytes(record.Partition.Value.ToString()) },
                                { "original-offset", Encoding.UTF8.GetBytes(record.Offset.Value.ToString()) },
                                { "failure", Encoding.UTF8.GetBytes((failure ?? "Unknown")[..Math.Min(failure?.Length ?? 7, 500)]) } };
                            await producer.ProduceAsync(options.InputTopic + ".dlq", new Message<string, string> { Key = record.Message.Key, Value = record.Message.Value, Headers = headers }, ct);
                            _deadLetters.Add(1); confirmed = true;
                        }
                        catch (ProduceException<string, string> error) { logger.LogWarning(error, "DLQ indisponivel; offset original nao confirmado."); await Task.Delay(1000, ct); }
                    }
                    if (!confirmed) break;
                }
                try
                {
                    consumer.Commit(record);
                    var watermarks = consumer.GetWatermarkOffsets(record.TopicPartition);
                    _consumerLag.Record(Math.Max(0, watermarks.High.Value - record.Offset.Value - 1));
                }
                catch (KafkaException error) { logger.LogWarning(error, "Offset nao confirmado; a Inbox protege contra reentrega."); }
            }
        }
        finally { consumer.Close(); }
    }

    private static Headers TraceHeaders(MessageEnvelope<FlowPayload> message)
    {
        var headers = new Headers { { "correlation-id", Encoding.UTF8.GetBytes(message.CorrelationId) } };
        if (message.TraceParent is not null) headers.Add("traceparent", Encoding.UTF8.GetBytes(message.TraceParent));
        if (message.TraceState is not null) headers.Add("tracestate", Encoding.UTF8.GetBytes(message.TraceState));
        return headers;
    }
    private static Activity? StartActivity(string operation, MessageEnvelope<FlowPayload> message)
    {
        ActivityContext.TryParse(message.TraceParent, message.TraceState, out var parent);
        var activity = Traces.StartActivity(operation, operation == "kafka.consume" ? ActivityKind.Consumer : ActivityKind.Producer, parent);
        activity?.SetBaggage("correlation.id", message.CorrelationId);
        return activity;
    }
    public override void Dispose() { _meter.Dispose(); base.Dispose(); }
}

public sealed class KafkaHealthCheck(KafkaOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Enabled) return Task.FromResult(HealthCheckResult.Healthy());
        try
        {
            using var client = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = options.BootstrapServers }).Build();
            var metadata = client.GetMetadata(TimeSpan.FromSeconds(3));
            return Task.FromResult(metadata.Brokers.Count > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        }
        catch (KafkaException) { return Task.FromResult(HealthCheckResult.Unhealthy()); }
    }
}
