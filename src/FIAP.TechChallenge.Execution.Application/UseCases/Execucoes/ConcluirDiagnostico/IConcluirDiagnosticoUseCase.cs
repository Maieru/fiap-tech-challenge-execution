using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirDiagnostico;

public interface IConcluirDiagnosticoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(ConcluirDiagnosticoCommand command, CancellationToken cancellationToken = default);
}
