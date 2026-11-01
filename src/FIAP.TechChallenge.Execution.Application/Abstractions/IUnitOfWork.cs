namespace FIAP.TechChallenge.Execution.Application.Abstractions;

public interface IUnitOfWork
{
    Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default);
}

public interface ITransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
