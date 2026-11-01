using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Maieru.FiapTechChallenge.Chassis;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.CancelarExecucao;

public sealed class CancelarExecucaoUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork,
    IProcessedMessageRepository processed, IMessagingService messaging, MessageContext context,
    IPecaInsumoRepository parts, TimeProvider clock) : ICancelarExecucaoUseCase
{
    public async Task<Result<bool>> ExecuteAsync(CancelarExecucaoCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);

        if (context.IncomingMessageId is Guid messageId && await processed.ExistsAsync(messageId, cancellationToken))
            return Result<bool>.Success(true);

        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (!result.IsSuccess)
            return Result<bool>.Failure(result.Error);

        var order = result.Value!;

        if (order.Status == StatusExecucao.Cancelada)
        {
            if (context.IncomingMessageId is Guid duplicate)
                await processed.AddAsync(duplicate, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }

        var now = clock.GetUtcNow();
        var changed = order.Cancelar(now);

        if (!changed.IsSuccess)
            return Result<bool>.Failure(changed.Error);

        foreach (var item in order.PecasInsumos)
        {
            var part = await parts.GetIncludingInactiveAsync(item.PecaInsumoId, cancellationToken);

            if (!part.IsSuccess)
                return Result<bool>.Failure(part.Error);

            var returned = part.Value!.AddEstoque(item.Quantidade);

            if (!returned.IsSuccess)
                return Result<bool>.Failure(returned.Error);

            await parts.UpdateAsync(part.Value, cancellationToken);
        }
        await repository.UpdateAsync(order, cancellationToken);

        if (context.IncomingMessageId is Guid incoming)
            await processed.AddAsync(incoming, cancellationToken);

        await messaging.PublishAsync(ExecutionMessaging.Topic, order.OrdemServicoId.ToString("D"),
            context.Create(ExecucaoCancelada.Type, new ExecucaoCancelada(order.OrdemServicoId), now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
