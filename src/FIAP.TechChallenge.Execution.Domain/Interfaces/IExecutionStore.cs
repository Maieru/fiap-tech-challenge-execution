using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Interfaces;

public interface IExecutionRepository
{
    Task<Execucao?> GetAsync(Guid ordemId, CancellationToken ct);
    Task<(IReadOnlyCollection<Execucao> Items, int Total)> ListAsync(EstadoExecucao? estado, int page, int size, CancellationToken ct);
    Task AddAsync(Execucao entity, CancellationToken ct);
    Task SaveAsync(Execucao entity, long expectedVersion, CancellationToken ct);
}
public interface ITransactionRunner
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> action, CancellationToken ct);
}
public sealed record StoredCommand(string RequestHash, string ResponseJson);
public interface ICommandStore
{
    Task<StoredCommand?> GetAsync(string id, CancellationToken ct);
    Task AddAsync(string id, StoredCommand value, CancellationToken ct);
}
public interface IReservations
{
    Task AddAsync(Guid ordemId, Guid itemId, Guid pecaId, int quantidade, CancellationToken ct);
    Task ReleaseAsync(Guid ordemId, CancellationToken ct);
}
public interface IInbox
{
    Task<bool> ContainsAsync(Guid messageId, CancellationToken ct);
    Task AddAsync(Guid messageId, CancellationToken ct);
}
public interface IIntegrationEvents
{
    Task EnqueueAsync(string type, Guid ordemId, long sequence, object? snapshot, Guid? requestId, string? reason, CancellationToken ct);
}
public interface IServicoDaOrdemDeServicoRepository
{
    Task<Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>> GetConcluidosByServicoIdAsync(Guid servicoId, CancellationToken cancellationToken = default);
}
public sealed class OperationConflictException(string message) : Exception(message);
