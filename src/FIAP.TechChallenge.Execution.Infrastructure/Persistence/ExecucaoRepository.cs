using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class ExecucaoRepository(MongoStore store) : IExecucaoRepository
{
    private readonly Dictionary<Guid, long> versions = [];
    private IMongoCollection<ExecucaoDocument> Collection => store.Collection<ExecucaoDocument>("execucoes");

    public async Task<Result<Execucao>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await store.Find(Collection, Builders<ExecucaoDocument>.Filter.Eq(x => x.Id, id)).SingleOrDefaultAsync(cancellationToken);
        if (row is null)
            return Result<Execucao>.Failure(Error.NotFound("Execução"));
        versions[id] = row.Version;
        return Execucao.Rehydrate(row.Snapshot);
    }

    public async Task<(IReadOnlyList<Execucao> Items, long Total)> GetPagedAsync(
        StatusExecucao? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var filter = status.HasValue ? Builders<ExecucaoDocument>.Filter.Eq(x => x.Status, status.Value) : Builders<ExecucaoDocument>.Filter.Empty;
        var query = store.Find(Collection, filter);
        var count = await query.CountDocumentsAsync(cancellationToken);
        var rows = await query.SortByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(checked((pageNumber - 1) * pageSize)).Limit(pageSize).ToListAsync(cancellationToken);
        return (rows.Select(x => Execucao.Rehydrate(x.Snapshot).Value!).ToArray(), count);
    }

    public Task AddAsync(Execucao order, CancellationToken cancellationToken = default) =>
        store.InsertAsync(Collection, Document(order, 0), cancellationToken);

    public async Task UpdateAsync(Execucao order, CancellationToken cancellationToken = default)
    {
        var version = versions[order.OrdemServicoId];
        var result = await store.ReplaceAsync(Collection,
            Builders<ExecucaoDocument>.Filter.Where(x => x.Id == order.OrdemServicoId && x.Version == version),
            Document(order, version + 1), cancellationToken);
        if (result.MatchedCount != 1)
            throw new ConcurrencyException();
        versions[order.OrdemServicoId] = version + 1;
    }

    private static ExecucaoDocument Document(Execucao order, long version) => new()
    {
        Id = order.OrdemServicoId,
        CreatedAtUtc = order.Snapshot().DataCriacaoUtc.UtcDateTime,
        Status = order.Status,
        Snapshot = order.Snapshot(),
        Version = version
    };
}

public sealed class ProcessedMessageRepository(MongoStore store) : IProcessedMessageRepository
{
    private IMongoCollection<ProcessedMessageDocument> Collection => store.Collection<ProcessedMessageDocument>("mensagens_processadas");
    private static string Key(Guid messageId) => $"oficina-execution-v1/{messageId:D}";
    public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        store.Find(Collection, Builders<ProcessedMessageDocument>.Filter.Eq(x => x.Id, Key(messageId))).AnyAsync(cancellationToken);
    public Task AddAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        store.InsertAsync(Collection, new ProcessedMessageDocument { Id = Key(messageId), ProcessedAtUtc = DateTime.UtcNow }, cancellationToken);
}
