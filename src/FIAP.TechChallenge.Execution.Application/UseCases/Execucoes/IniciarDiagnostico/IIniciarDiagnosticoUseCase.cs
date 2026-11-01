using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarDiagnostico;

public interface IIniciarDiagnosticoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(IniciarDiagnosticoCommand command, CancellationToken cancellationToken = default);
}
