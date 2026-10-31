using MongoDB.Bson;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoBootstrap(IMongoDatabase database)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        var existing = await (await database.ListCollectionNamesAsync(cancellationToken: ct)).ToListAsync(ct);
        foreach (var name in new[] { "servicos", "pecas", "execucoes", "reservas", "commands", "outbox", "inbox", "leases" })
        {
            if (existing.Contains(name)) continue;
            try
            {
                var options = name == "pecas" ? new CreateCollectionOptions<BsonDocument>
                { Validator = new BsonDocument("QuantidadeEstoque", new BsonDocument("$gte", 0)) } : null;
                if (options is null) await database.CreateCollectionAsync(name, cancellationToken: ct);
                else await database.CreateCollectionAsync(name, options, ct);
            }
            catch (MongoCommandException error) when (error.Code == 48) { } // concurrent bootstrap
        }
        await database.GetCollection<PecaDocument>("pecas").Indexes.CreateOneAsync(new CreateIndexModel<PecaDocument>(
            Builders<PecaDocument>.IndexKeys.Ascending(x => x.Codigo), new CreateIndexOptions<PecaDocument>
            { Unique = true, PartialFilterExpression = Builders<PecaDocument>.Filter.Eq(x => x.Ativo, true), Name = "codigo_ativo" }), cancellationToken: ct);
        await database.GetCollection<ExecucaoDocument>("execucoes").Indexes.CreateManyAsync([
            new(Builders<ExecucaoDocument>.IndexKeys.Ascending(x => x.OrdemServicoId), new CreateIndexOptions { Unique = true }),
            new(Builders<ExecucaoDocument>.IndexKeys.Ascending(x => x.Estado).Ascending(x => x.DataCriacao).Ascending(x => x.OrdemServicoId))
        ], ct);
        await database.GetCollection<ReservaDocument>("reservas").Indexes.CreateOneAsync(new CreateIndexModel<ReservaDocument>(
            Builders<ReservaDocument>.IndexKeys.Ascending(x => x.OrdemServicoId).Ascending(x => x.Devolvida)), cancellationToken: ct);
        await database.GetCollection<OutboxDocument>("outbox").Indexes.CreateOneAsync(new CreateIndexModel<OutboxDocument>(
            Builders<OutboxDocument>.IndexKeys.Ascending(x => x.Published).Ascending(x => x.Sequence).Ascending(x => x.CreatedAtUtc)), cancellationToken: ct);
    }
}
