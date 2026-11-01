using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarServico;

public interface IAdicionarServicoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(AdicionarServicoCommand command, CancellationToken cancellationToken = default);
}