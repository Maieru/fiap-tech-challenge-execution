using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarPecaInsumo;

public sealed class AdicionarPecaInsumoUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork, IPecaInsumoRepository catalog, TimeProvider clock) : IAdicionarPecaInsumoUseCase
{
    public async Task<Result<ExecucaoResponse>> ExecuteAsync(AdicionarPecaInsumoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrdemServicoId == Guid.Empty)
            return Result<ExecucaoResponse>.Failure(Error.InvalidId("Ordem"));

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (!result.IsSuccess)
            return Result<ExecucaoResponse>.Failure(result.Error);

        var order = result.Value!;
        var now = clock.GetUtcNow();
        var item = await catalog.GetByIdAsync(command.PecaInsumoId, cancellationToken);

        if (!item.IsSuccess)
            return Result<ExecucaoResponse>.Failure(item.Error);

        var changed = order.AdicionarPeca(item.Value!, command.Quantidade);

        if (!changed.IsSuccess)
            return Result<ExecucaoResponse>.Failure(changed.Error);

        var stock = item.Value!.RemoveEstoque(command.Quantidade);

        if (!stock.IsSuccess)
            return Result<ExecucaoResponse>.Failure(stock.Error);

        await catalog.UpdateAsync(item.Value, cancellationToken);

        if (!changed.IsSuccess)
            return Result<ExecucaoResponse>.Failure(changed.Error);

        await repository.UpdateAsync(order, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ExecucaoResponse>.Success(ExecucaoResponse.From(order));
    }
}
