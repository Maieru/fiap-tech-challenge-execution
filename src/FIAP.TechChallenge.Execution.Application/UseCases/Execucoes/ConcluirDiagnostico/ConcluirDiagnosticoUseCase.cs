using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Maieru.FiapTechChallenge.Chassis;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirDiagnostico;

public sealed class ConcluirDiagnosticoUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork,
    IMessagingService messaging, MessageContext context, TimeProvider clock) : IConcluirDiagnosticoUseCase
{
    public async Task<Result<ExecucaoResponse>> ExecuteAsync(ConcluirDiagnosticoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrdemServicoId == Guid.Empty)
            return Result<ExecucaoResponse>.Failure(Error.InvalidId("Ordem"));

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var result = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (!result.IsSuccess)
            return Result<ExecucaoResponse>.Failure(result.Error);

        var order = result.Value!;
        var now = clock.GetUtcNow();
        var changed = order.ConcluirDiagnostico(now);

        if (!changed.IsSuccess)
            return Result<ExecucaoResponse>.Failure(changed.Error);

        await repository.UpdateAsync(order, cancellationToken);

        await messaging.PublishAsync(ExecutionMessaging.Topic, order.OrdemServicoId.ToString("D"),
            context.Create(DiagnosticoConcluido.Type, new DiagnosticoConcluido(order.OrdemServicoId,
            order.Servicos.Select(x => new ServicoDiagnostico(x.Id, x.ServicoId, x.Descricao, x.ValorUnitario, x.Quantidade, x.ValorTotal)).ToArray(),
            order.PecasInsumos.Select(x => new PecaInsumoDiagnostico(x.Id, x.PecaInsumoId, x.Nome, x.Codigo, x.Descricao, x.PrecoUnitario, x.Quantidade, x.ValorTotal)).ToArray(),
            order.ValorTotal), now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<ExecucaoResponse>.Success(ExecucaoResponse.From(order));
    }
}
