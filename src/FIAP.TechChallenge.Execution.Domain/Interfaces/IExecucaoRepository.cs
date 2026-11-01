using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Interfaces;

public interface IExecucaoRepository
{
    Task<Result<Execucao>> GetByIdAsync(Guid ordemServicoId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Execucao> Items, long Total)> GetPagedAsync(StatusExecucao? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(Execucao execucao, CancellationToken cancellationToken = default);
    Task UpdateAsync(Execucao execucao, CancellationToken cancellationToken = default);
}

public interface IProcessedMessageRepository
{
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task AddAsync(Guid messageId, CancellationToken cancellationToken = default);
}
