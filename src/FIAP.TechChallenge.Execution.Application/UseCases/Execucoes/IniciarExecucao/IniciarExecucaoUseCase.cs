using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Maieru.FiapTechChallenge.Chassis;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarExecucao;

public sealed record IniciarExecucaoCommand(Guid OrdemServicoId);
public interface IIniciarExecucaoUseCase
{
    Task<Result<bool>> ExecuteAsync(IniciarExecucaoCommand command, CancellationToken cancellationToken = default);
}
public sealed class IniciarExecucaoUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork,
    IProcessedMessageRepository processed, IMessagingService messaging, MessageContext context,
    TimeProvider clock) : IIniciarExecucaoUseCase
{
    public async Task<Result<bool>> ExecuteAsync(IniciarExecucaoCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);

        if (context.IncomingMessageId is Guid messageId && await processed.ExistsAsync(messageId, cancellationToken))
            return Result<bool>.Success(true);

        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (!result.IsSuccess)
            return Result<bool>.Failure(result.Error);

        var order = result.Value!;

        if (order.Status is StatusExecucao.EmExecucao or StatusExecucao.Finalizada)
        {
            if (context.IncomingMessageId is Guid duplicate)
                await processed.AddAsync(duplicate, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<bool>.Success(true);
        }

        var now = clock.GetUtcNow();
        var changed = order.IniciarExecucao(now);

        if (!changed.IsSuccess)
            return Result<bool>.Failure(changed.Error);

        await repository.UpdateAsync(order, cancellationToken);

        if (context.IncomingMessageId is Guid incoming)
            await processed.AddAsync(incoming, cancellationToken);

        await messaging.PublishAsync(ExecutionMessaging.Topic, order.OrdemServicoId.ToString("D"),
            context.Create(ExecucaoIniciada.Type, new ExecucaoIniciada(order.OrdemServicoId), now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
