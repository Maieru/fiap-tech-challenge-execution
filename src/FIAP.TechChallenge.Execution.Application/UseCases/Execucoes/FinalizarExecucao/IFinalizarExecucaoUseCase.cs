using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.FinalizarExecucao;

public interface IFinalizarExecucaoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(FinalizarExecucaoCommand command, CancellationToken cancellationToken = default);
}
