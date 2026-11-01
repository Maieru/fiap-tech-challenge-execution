using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RecuperarExecucao;

public sealed class RecuperarExecucaoUseCase(IExecucaoRepository repository) : IRecuperarExecucaoUseCase
{
    public async Task<Result<ExecucaoResponse>> ExecuteAsync(RecuperarExecucaoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrdemServicoId == Guid.Empty)
            return Result<ExecucaoResponse>.Failure(Error.InvalidId("Ordem"));

        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        return result.IsSuccess ? Result<ExecucaoResponse>.Success(ExecucaoResponse.From(result.Value!))
            : Result<ExecucaoResponse>.Failure(result.Error);
    }
}
