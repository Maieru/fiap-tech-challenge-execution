using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.CadastrarServico;

public interface ICadastrarServicoUseCase
{
    Task<Result<CadastrarServicoResponse>> ExecuteAsync(CadastrarServicoCommand command, CancellationToken cancellationToken = default);
}

