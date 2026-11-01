using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirServico;

public interface IConcluirServicoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(ConcluirServicoCommand command, CancellationToken cancellationToken = default);
}
