using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Interfaces;

public interface IPecaInsumoRepository
{
    Task<Result<PecaInsumo>> GetIncludingInactiveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<Result<PecaInsumo>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PecaInsumo>> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<Result<(IReadOnlyCollection<PecaInsumo> PecasInsumos, int TotalItems)>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task AddAsync(PecaInsumo pecaInsumo, CancellationToken cancellationToken = default);
    Task UpdateAsync(PecaInsumo pecaInsumo, CancellationToken cancellationToken = default);
    Task DeleteAsync(PecaInsumo pecaInsumo, CancellationToken cancellationToken = default);
}

