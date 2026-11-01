using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RegistrarOrdemRecebida;

public interface IRegistrarOrdemRecebidaUseCase
{
    Task<Result<bool>> ExecuteAsync(RegistrarOrdemRecebidaCommand command, CancellationToken cancellationToken = default);
}
