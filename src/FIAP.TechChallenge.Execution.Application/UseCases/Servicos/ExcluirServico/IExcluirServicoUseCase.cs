using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ExcluirServico;

public interface IExcluirServicoUseCase
{
    Task<Result<ExcluirServicoResponse>> ExecuteAsync(ExcluirServicoCommand command, CancellationToken cancellationToken = default);
}
