using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.AtualizarServico;

public interface IAtualizarServicoUseCase
{
    Task<Result<AtualizarServicoResponse>> ExecuteAsync(AtualizarServicoCommand command, CancellationToken cancellationToken = default);
}

