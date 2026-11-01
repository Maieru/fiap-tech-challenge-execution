using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.AtualizarPecaInsumo;

public interface IAtualizarPecaInsumoUseCase
{
    Task<Result<AtualizarPecaInsumoResponse>> ExecuteAsync(AtualizarPecaInsumoCommand command, CancellationToken cancellationToken = default);
}

