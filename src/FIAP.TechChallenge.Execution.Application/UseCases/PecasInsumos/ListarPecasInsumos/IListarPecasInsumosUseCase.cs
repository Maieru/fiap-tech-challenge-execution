using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;

public interface IListarPecasInsumosUseCase
{
    Task<Result<ListarPecasInsumosResponse>> ExecuteAsync(ListarPecasInsumosCommand command, CancellationToken cancellationToken = default);
}
