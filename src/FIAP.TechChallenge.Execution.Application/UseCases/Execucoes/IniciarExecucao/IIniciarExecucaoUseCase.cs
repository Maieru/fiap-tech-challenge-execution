using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarExecucao;

public interface IIniciarExecucaoUseCase
{
    Task<Result<bool>> ExecuteAsync(IniciarExecucaoCommand command, CancellationToken cancellationToken = default);
}
