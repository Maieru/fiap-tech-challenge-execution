using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ExcluirPecaInsumo;

public interface IExcluirPecaInsumoUseCase
{
    Task<Result<ExcluirPecaInsumoResponse>> ExecuteAsync(ExcluirPecaInsumoCommand command, CancellationToken cancellationToken = default);
}
