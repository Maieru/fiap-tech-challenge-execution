using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoInitializationService(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var existing = await (await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken);
        foreach (var name in new[] { "servicos", "pecas_insumos", "execucoes", "mensagens_processadas" })
            if (!existing.Contains(name)) await database.CreateCollectionAsync(name, cancellationToken: cancellationToken);
        await database.GetCollection<PecaDocument>("pecas_insumos").Indexes.CreateOneAsync(
            new CreateIndexModel<PecaDocument>(Builders<PecaDocument>.IndexKeys.Ascending(x => x.Codigo),
                new CreateIndexOptions<PecaDocument> { Name = "codigo_ativo_unico", Unique = true,
                    PartialFilterExpression = Builders<PecaDocument>.Filter.Eq(x => x.Ativo, true) }), cancellationToken: cancellationToken);
        await database.GetCollection<ExecucaoDocument>("execucoes").Indexes.CreateOneAsync(
            new CreateIndexModel<ExecucaoDocument>(Builders<ExecucaoDocument>.IndexKeys.Ascending(x => x.Status)
                .Descending(x => x.CreatedAtUtc).Ascending(x => x.Id)), cancellationToken: cancellationToken);
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
