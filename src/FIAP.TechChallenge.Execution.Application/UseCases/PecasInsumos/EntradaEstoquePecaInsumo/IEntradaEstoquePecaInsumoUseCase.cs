using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;

public interface IEntradaEstoquePecaInsumoUseCase
{
    Task<Result<EntradaEstoquePecaInsumoResponse>> ExecuteAsync(EntradaEstoquePecaInsumoCommand command, CancellationToken cancellationToken = default);
}

