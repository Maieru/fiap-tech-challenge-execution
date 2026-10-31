using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.VerificarTempoMedioServico;

public interface IVerificarTempoMedioServicoUseCase
{
    Task<Result<VerificarTempoMedioServicoResponse>> ExecuteAsync(VerificarTempoMedioServicoCommand command, CancellationToken cancellationToken = default);
}
