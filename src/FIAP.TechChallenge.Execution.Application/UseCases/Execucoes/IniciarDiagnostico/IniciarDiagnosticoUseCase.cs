using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Maieru.FiapTechChallenge.Chassis;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarDiagnostico;

public sealed class IniciarDiagnosticoUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork,
    IMessagingService messaging, MessageContext context, TimeProvider clock) : IIniciarDiagnosticoUseCase
{
    public async Task<Result<ExecucaoResponse>> ExecuteAsync(IniciarDiagnosticoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrdemServicoId == Guid.Empty)
            return Result<ExecucaoResponse>.Failure(Error.InvalidId("Ordem"));

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (!result.IsSuccess)
            return Result<ExecucaoResponse>.Failure(result.Error);

        var order = result.Value!;
        var now = clock.GetUtcNow();
        var changed = order.IniciarDiagnostico(now);

        if (!changed.IsSuccess)
            return Result<ExecucaoResponse>.Failure(changed.Error);

        await repository.UpdateAsync(order, cancellationToken);
        await messaging.PublishAsync(ExecutionMessaging.Topic, order.OrdemServicoId.ToString("D"),
            context.Create(DiagnosticoIniciado.Type, new DiagnosticoIniciado(order.OrdemServicoId), now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result<ExecucaoResponse>.Success(ExecucaoResponse.From(order));
    }
}
