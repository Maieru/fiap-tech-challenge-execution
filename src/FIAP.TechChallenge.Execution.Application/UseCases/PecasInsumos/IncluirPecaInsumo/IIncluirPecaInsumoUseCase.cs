using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.IncluirPecaInsumo;

public interface IIncluirPecaInsumoUseCase
{
    Task<Result<IncluirPecaInsumoResponse>> ExecuteAsync(IncluirPecaInsumoCommand command, CancellationToken cancellationToken = default);
}
