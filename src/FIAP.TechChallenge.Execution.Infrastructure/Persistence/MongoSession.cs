using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoSession(IMongoClient client) : ITransactionRunner
{
    public IClientSessionHandle? Current { get; private set; }
    public Dictionary<string, long> Versions { get; } = [];

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> action, CancellationToken ct)
    {
        if (Current is not null) return await action(ct);
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        Current = session;
        try
        {
            return await session.WithTransactionAsync(async (_, token) =>
            {
                Versions.Clear();
                var result = await action(token);
                if (!result.IsSuccess) throw new Rollback<T>(result);
                return result;
            }, new TransactionOptions(readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), ct);
        }
        catch (Rollback<T> error) { return error.Result; }
        catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
        { throw new OperationConflictException("Operacao concorrente ou cadastro ativo duplicado. Repita a solicitacao com a mesma chave."); }
        finally { Current = null; Versions.Clear(); }
    }
    private sealed class Rollback<T>(Result<T> result) : Exception { public Result<T> Result { get; } = result; }

    public Task<List<T>> ReadAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter, CancellationToken ct) =>
        Current is null ? collection.Find(filter).ToListAsync(ct) : collection.Find(Current, filter).ToListAsync(ct);
    public async Task<(List<T> Items, int Total)> ReadPageAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter,
        SortDefinition<T> sort, int page, int size, CancellationToken ct)
    {
        var total = Current is null ? await collection.CountDocumentsAsync(filter, cancellationToken: ct)
            : await collection.CountDocumentsAsync(Current, filter, cancellationToken: ct);
        var query = Current is null ? collection.Find(filter) : collection.Find(Current, filter);
        var items = await query.Sort(sort).Skip(checked((page - 1) * size)).Limit(size).ToListAsync(ct);
        return (items, checked((int)total));
    }
    public Task InsertAsync<T>(IMongoCollection<T> collection, T value, CancellationToken ct) =>
        Current is null ? collection.InsertOneAsync(value, cancellationToken: ct) : collection.InsertOneAsync(Current, value, cancellationToken: ct);
    public Task<ReplaceOneResult> ReplaceAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter, T value, CancellationToken ct) =>
        Current is null ? collection.ReplaceOneAsync(filter, value, cancellationToken: ct) : collection.ReplaceOneAsync(Current, filter, value, cancellationToken: ct);
    public Task<UpdateResult> UpdateAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter, UpdateDefinition<T> update, CancellationToken ct) =>
        Current is null ? collection.UpdateOneAsync(filter, update, cancellationToken: ct) : collection.UpdateOneAsync(Current, filter, update, cancellationToken: ct);
}
