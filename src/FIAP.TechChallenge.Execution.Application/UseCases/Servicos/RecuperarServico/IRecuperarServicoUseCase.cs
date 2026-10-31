using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.RecuperarServico;

public interface IRecuperarServicoUseCase
{
    Task<Result<RecuperarServicoResponse>> ExecuteAsync(RecuperarServicoCommand command, CancellationToken cancellationToken = default);
}
