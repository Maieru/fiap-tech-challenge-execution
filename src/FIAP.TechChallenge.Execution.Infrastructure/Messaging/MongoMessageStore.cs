using System.Text.Json;
using System.Diagnostics.Metrics;
using FIAP.TechChallenge.Execution.Application;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Messaging;

public sealed class MongoMessageStore(IMongoDatabase database, MongoSession session, IntegrationContext context, IServiceProvider provider)
    : IOutboxStore, IIntegrationEventHandler, IInbox, IIntegrationEvents
{
    private static readonly Meter Metrics = new(KafkaWorker.InstrumentationName);
    private static readonly Counter<long> Compensations = Metrics.CreateCounter<long>("execution.stock.compensations");
    private readonly IMongoCollection<OutboxDocument> _outbox = database.GetCollection<OutboxDocument>("outbox");
    private readonly IMongoCollection<InboxDocument> _inbox = database.GetCollection<InboxDocument>("inbox");
    public Task EnqueueAsync(string type, Guid ordemId, long sequence, object? snapshot, Guid? requestId, string? reason, CancellationToken ct)
    {
        var payload = new FlowPayload(ordemId, sequence, SolicitacaoId: requestId,
            Snapshot: snapshot is null ? null : JsonSerializer.SerializeToElement(snapshot, IntegrationContext.Json), Motivo: reason);
        var envelope = context.Create(type, payload);
        return session.InsertAsync(_outbox, new OutboxDocument { Id = envelope.MessageId, OrdemServicoId = ordemId,
            Sequence = sequence, Body = IntegrationContext.Serialize(envelope), CreatedAtUtc = DateTime.UtcNow }, ct);
    }
    public async Task<bool> ContainsAsync(Guid messageId, CancellationToken ct) =>
        (await session.ReadAsync(_inbox, Builders<InboxDocument>.Filter.Eq(x => x.Id, messageId), ct)).Count > 0;
    public Task AddAsync(Guid messageId, CancellationToken ct) => session.InsertAsync(_inbox,
        new InboxDocument { Id = messageId, ProcessedAtUtc = DateTime.UtcNow,
            Body = context.Incoming is null ? null : IntegrationContext.Serialize(context.Incoming) }, ct);
    public async Task<bool> AcquireLeaseAsync(string owner, CancellationToken ct)
    {
        var items = database.GetCollection<DispatcherLease>("leases");
        var now = DateTime.UtcNow;
        try
        {
            var result = await items.FindOneAndUpdateAsync(x => x.Id == "dispatcher" && (x.UntilUtc < now || x.Owner == owner),
                Builders<DispatcherLease>.Update.Set(x => x.Owner, owner).Set(x => x.UntilUtc, now.AddSeconds(20)),
                new FindOneAndUpdateOptions<DispatcherLease> { IsUpsert = true, ReturnDocument = ReturnDocument.After }, ct);
            return result.Owner == owner;
        }
        catch (MongoCommandException error) when (error.Code == 11000) { return false; }
        catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey) { return false; }
    }
    public async Task<PendingMessage?> NextAsync(CancellationToken ct)
    {
        var row = await _outbox.Find(x => !x.Published).SortBy(x => x.Sequence).ThenBy(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        return row is null ? null : new(row.Id, row.OrdemServicoId, row.Sequence, row.Body);
    }
    public Task MarkPublishedAsync(Guid id, CancellationToken ct) => _outbox.UpdateOneAsync(x => x.Id == id,
        Builders<OutboxDocument>.Update.Set(x => x.Published, true), cancellationToken: ct);
    public Task<long> PendingCountAsync(CancellationToken ct) => _outbox.CountDocumentsAsync(x => !x.Published, cancellationToken: ct);
    public async Task ApplyAsync(MessageEnvelope<FlowPayload> message, CancellationToken ct)
    {
        var compensated = await provider.GetRequiredService<ProcessarEventoUseCase>().ExecuteAsync(new IncomingEvent(message.MessageId, message.Type,
            message.Payload.OrdemServicoId, message.Payload.Sequence, message.OccurredAtUtc.UtcDateTime, message.Payload.ClienteId,
            message.Payload.VeiculoId, message.Payload.DescricaoProblema, message.Payload.SolicitacaoId, message.Payload.OperadorId), ct);
        if (compensated) Compensations.Add(1);
    }
}
