using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

// Runs only in an explicitly isolated compatibility database, never application data.
public static partial class PersistenceCompatibility
{
    [GeneratedRegex("^fiap_compat_[a-z0-9_]{1,40}$")]
    private static partial Regex IsolatedDatabase();

    public static async Task VerifyAsync(IServiceProvider services, CancellationToken token = default)
    {
        var database = services.GetRequiredService<IMongoDatabase>();
        if (!IsolatedDatabase().IsMatch(database.DatabaseNamespace.DatabaseName))
            throw new InvalidOperationException("A verificação exige um banco isolado fiap_compat_<run>.");
        var client = services.GetRequiredService<IMongoClient>();
        try
        {
            await new MongoInitializationService(database).StartAsync(token);
            var parts = database.GetCollection<PecaDocument>("pecas_insumos");
            var messages = database.GetCollection<ProcessedMessageDocument>("mensagens_processadas");
            var id = Guid.NewGuid();
            await parts.InsertOneAsync(new PecaDocument { Id = id, Codigo = "compat", Ativo = true, QuantidadeEstoque = 1 }, cancellationToken: token);
            try
            {
                await parts.InsertOneAsync(new PecaDocument { Id = Guid.NewGuid(), Codigo = "compat", Ativo = true }, cancellationToken: token);
                throw new InvalidOperationException("Índice único parcial não rejeitou duplicidade.");
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
            await parts.InsertOneAsync(new PecaDocument { Id = Guid.NewGuid(), Codigo = "compat", Ativo = false }, cancellationToken: token);

            // Debit and deduplication roll back together when the transaction is aborted.
            await using (var scope = services.CreateAsyncScope())
            {
                var work = scope.ServiceProvider.GetRequiredService<MongoUnitOfWork>();
                await work.BeginAsync(token);
                await parts.UpdateOneAsync(work.Session!, x => x.Id == id, Builders<PecaDocument>.Update.Set(x => x.QuantidadeEstoque, 0), cancellationToken: token);
                await messages.InsertOneAsync(work.Session!, new ProcessedMessageDocument { Id = "rollback" }, cancellationToken: token);
            }
            if ((await parts.Find(x => x.Id == id).SingleAsync(token)).QuantidadeEstoque != 1 ||
                await messages.Find(x => x.Id == "rollback").AnyAsync(token))
                throw new InvalidOperationException("Abort não restaurou estoque/deduplicação.");

            // A stale transaction must not overwrite the winner's debit.
            await using var firstScope = services.CreateAsyncScope();
            await using var secondScope = services.CreateAsyncScope();
            var first = firstScope.ServiceProvider.GetRequiredService<MongoUnitOfWork>();
            var second = secondScope.ServiceProvider.GetRequiredService<MongoUnitOfWork>();
            await first.BeginAsync(token);
            await second.BeginAsync(token);
            _ = await parts.Find(first.Session!, x => x.Id == id).SingleAsync(token);
            _ = await parts.Find(second.Session!, x => x.Id == id).SingleAsync(token);
            await parts.UpdateOneAsync(first.Session!, x => x.Id == id, Builders<PecaDocument>.Update.Set(x => x.QuantidadeEstoque, 0), cancellationToken: token);
            await messages.InsertOneAsync(first.Session!, new ProcessedMessageDocument { Id = "committed" }, cancellationToken: token);
            await first.CommitAsync(token);
            var conflict = false;
            try
            {
                await parts.UpdateOneAsync(second.Session!, x => x.Id == id, Builders<PecaDocument>.Update.Set(x => x.QuantidadeEstoque, -1), cancellationToken: token);
                await second.CommitAsync(token);
            }
            catch (MongoCommandException exception) when (exception.Code == 112 || exception.HasErrorLabel("TransientTransactionError")) { conflict = true; }
            catch (MongoWriteException exception) when (exception.WriteError.Code == 112 || exception.HasErrorLabel("TransientTransactionError")) { conflict = true; }
            if (!conflict || (await parts.Find(x => x.Id == id).SingleAsync(token)).QuantidadeEstoque != 0)
                throw new InvalidOperationException("Concorrência permitiu um segundo débito.");
            if (!await messages.Find(x => x.Id == "committed").AnyAsync(token))
                throw new InvalidOperationException("Commit não persistiu deduplicação.");
            try
            {
                await messages.InsertOneAsync(new ProcessedMessageDocument { Id = "committed" }, cancellationToken: token);
                throw new InvalidOperationException("Deduplicação não rejeitou replay.");
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
            Console.WriteLine("Compatibilidade de persistência: índices, commit, rollback, concorrência e deduplicação aprovados.");
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName, cleanup.Token);
        }
    }
}
