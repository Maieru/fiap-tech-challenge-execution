using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarPecaInsumo;

public interface IAdicionarPecaInsumoUseCase
{
    Task<Result<ExecucaoResponse>> ExecuteAsync(AdicionarPecaInsumoCommand command, CancellationToken cancellationToken = default);
}
