using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ListarExecucoes;

public interface IListarExecucoesUseCase
{
    Task<Result<ListarExecucoesResponse>> ExecuteAsync(ListarExecucoesCommand command, CancellationToken cancellationToken = default);
}
