using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RecuperarExecucao;

public interface IRecuperarExecucaoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(RecuperarExecucaoCommand command, CancellationToken cancellationToken = default);
}
