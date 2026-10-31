using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.RecuperarPecaInsumo;

public interface IRecuperarPecaInsumoUseCase
{
    Task<Result<RecuperarPecaInsumoResponse>> ExecuteAsync(RecuperarPecaInsumoCommand command, CancellationToken cancellationToken = default);
}
