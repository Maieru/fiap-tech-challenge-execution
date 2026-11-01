using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirServico;

public sealed class ConcluirServicoUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork, TimeProvider clock) : IConcluirServicoUseCase
{
    public async Task<Result<ExecucaoResponse>> ExecuteAsync(ConcluirServicoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrdemServicoId == Guid.Empty)
            return Result<ExecucaoResponse>.Failure(Error.InvalidId("Ordem"));

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (!result.IsSuccess)
            return Result<ExecucaoResponse>.Failure(result.Error);

        var order = result.Value!;
        var now = clock.GetUtcNow();
        var changed = order.ConcluirServico(command.ItemId, command.TempoGastoMinutos);

        if (!changed.IsSuccess)
            return Result<ExecucaoResponse>.Failure(changed.Error);

        await repository.UpdateAsync(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ExecucaoResponse>.Success(ExecucaoResponse.From(order));
    }
}
