using Confluent.Kafka;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.CancelarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RegistrarOrdemRecebida;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.OS.Application.Messaging;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace FIAP.TechChallenge.Execution.Infrastructure.Messaging;

public sealed class ConsumerState
{
    public bool Running { get; set; }
}

public sealed class ExecutionKafkaConsumer(IConfiguration configuration, IServiceScopeFactory scopes,
    ConsumerState state, ILogger<ExecutionKafkaConsumer> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = configuration["Kafka:GroupId"] ?? "oficina-execution-v1",
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = false,
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        ConsumeResult<string, string>? record = null;
        Guid? pendingMessageId = null;
        string? pendingCorrelationId = null;
        try
        {
            consumer.Subscribe(configuration["Kafka:InputTopic"] ?? OSMessaging.Topic);
            state.Running = true;
            while (!stoppingToken.IsCancellationRequested)
            {
                record = consumer.Consume(stoppingToken);
                pendingMessageId = null;
                pendingCorrelationId = null;
                if (record.IsPartitionEOF)
                    continue;
                var envelope = JsonSerializer.Deserialize<MessageEnvelope<JsonElement>>(record.Message.Value, JsonOptions)
                    ?? throw new InvalidOperationException("Envelope ausente.");
                pendingMessageId = envelope.MessageId;
                pendingCorrelationId = envelope.CorrelationId;
                if (envelope.MessageId == Guid.Empty || envelope.Version != OSMessaging.Version || envelope.OccurredAtUtc == default
                    || envelope.OccurredAtUtc.Offset != TimeSpan.Zero || !ValidCorrelation(envelope.CorrelationId))
                    throw new InvalidOperationException("Metadados de mensagem inválidos.");
                if (!Guid.TryParseExact(record.Message.Key, "D", out var key) || key == Guid.Empty)
                    throw new InvalidOperationException("Chave Kafka inválida.");

                using var logScope = logger.BeginScope(new Dictionary<string, object>
                {
                    ["MessageId"] = envelope.MessageId,
                    ["CorrelationId"] = envelope.CorrelationId,
                    ["Topic"] = record.Topic,
                    ["Partition"] = record.Partition.Value,
                    ["Offset"] = record.Offset.Value
                });
                using var activity = new Activity("execution.kafka.consume").SetIdFormat(ActivityIdFormat.W3C);
                if (envelope.TraceParent is not null)
                    _ = activity.SetParentId(envelope.TraceParent);
                activity.TraceStateString = envelope.TraceState;
                _ = activity.AddBaggage("correlation.id", envelope.CorrelationId);
                _ = activity.Start();
                await using var scope = scopes.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<MessageContext>();
                context.IncomingMessageId = envelope.MessageId;
                context.IncomingOccurredAtUtc = envelope.OccurredAtUtc;
                context.CorrelationId = envelope.CorrelationId;
                Result<bool> result;
                switch (envelope.Type, envelope.Version)
                {
                    case (OrdemServicoCriada.Type, OSMessaging.Version):
                        var created = Payload<OrdemServicoCriada>(envelope);
                        CheckKey(key, created.OrdemServicoId);
                        result = await scope.ServiceProvider.GetRequiredService<IRegistrarOrdemRecebidaUseCase>()
                            .ExecuteAsync(new(created.OrdemServicoId, created.ClienteId, created.VeiculoId, created.DescricaoProblema), stoppingToken);
                        break;
                    case (ExecucaoAutorizada.Type, OSMessaging.Version):
                        var authorized = Payload<ExecucaoAutorizada>(envelope);
                        CheckKey(key, authorized.OrdemServicoId);
                        result = await scope.ServiceProvider.GetRequiredService<IIniciarExecucaoUseCase>()
                            .ExecuteAsync(new(authorized.OrdemServicoId), stoppingToken);
                        break;
                    case (CancelamentoSolicitado.Type, OSMessaging.Version):
                        var cancelled = Payload<CancelamentoSolicitado>(envelope);
                        CheckKey(key, cancelled.OrdemServicoId);
                        result = await scope.ServiceProvider.GetRequiredService<ICancelarExecucaoUseCase>()
                            .ExecuteAsync(new(cancelled.OrdemServicoId), stoppingToken);
                        break;
                    default:
                        throw new InvalidOperationException("Tipo ou versão de mensagem não suportado.");
                }
                if (!result.IsSuccess)
                    throw new InvalidOperationException($"Caso de uso recusou a mensagem: {result.Error.Description}");
                consumer.Commit(record);
                logger.LogInformation("Mensagem processada e offset confirmado.");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Critical))
            {
                logger.LogCritical(exception, "Consumo interrompido sem avançar o offset pendente. Tópico {Topic}, partição {Partition}, offset {Offset}, mensagem {MessageId}, correlação {CorrelationId}. Corrija a causa e reinicie.",
                    record?.Topic, record?.Partition.Value, record?.Offset.Value, pendingMessageId, pendingCorrelationId);
            }
            Environment.ExitCode = 1;
            throw;
        }
        finally
        {
            state.Running = false;
            try
            { consumer.Close(); }
            catch (KafkaException exception) { logger.LogError(exception, "Falha ao fechar o consumidor."); }
        }
    }

    private static T Payload<T>(MessageEnvelope<JsonElement> envelope) =>
        envelope.Payload.Deserialize<T>(JsonOptions) ?? throw new InvalidOperationException("Payload ausente.");
    private static void CheckKey(Guid key, Guid id)
    {
        if (key != id)
            throw new InvalidOperationException("A chave Kafka não corresponde à ordem do payload.");
    }
    private static bool ValidCorrelation(string? value) => value is { Length: > 0 and <= 128 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
}
