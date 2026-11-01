using FIAP.TechChallenge.Execution.Application.Abstractions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoUnitOfWork(IMongoClient client, ILogger<MongoUnitOfWork> logger) : IUnitOfWork, ITransaction
{
    public IClientSessionHandle? Session { get; private set; }

    public async Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        if (Session is not null) throw new InvalidOperationException("Já existe uma transação neste escopo.");
        Session = await client.StartSessionAsync(cancellationToken: cancellationToken);
        Session.StartTransaction(new TransactionOptions(readConcern: ReadConcern.Snapshot,
            writeConcern: WriteConcern.WMajority, readPreference: ReadPreference.Primary,
            maxCommitTime: TimeSpan.FromSeconds(10)));
        return this;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null) throw new InvalidOperationException("Nenhuma transação aberta.");
        try { await Session.CommitTransactionAsync(cancellationToken); }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Falha no commit MongoDB. Verifique o resultado confirmado e as mensagens entregues antes de repetir a operação.");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        var session = Session;
        Session = null;
        if (session is null) return;
        try
        {
            if (session.IsInTransaction)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await session.AbortTransactionAsync(timeout.Token);
            }
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Não foi possível confirmar o abort da transação MongoDB. Reconciliação necessária.");
        }
        finally { session.Dispose(); }
    }
}
