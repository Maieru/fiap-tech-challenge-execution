using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.CancelarExecucao;

public interface ICancelarExecucaoUseCase
{
    Task<Result<bool>> ExecuteAsync(CancelarExecucaoCommand command, CancellationToken cancellationToken = default);
}
