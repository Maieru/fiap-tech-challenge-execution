using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RegistrarOrdemRecebida;

public sealed class RegistrarOrdemRecebidaUseCase(IExecucaoRepository repository, IUnitOfWork unitOfWork,
    IProcessedMessageRepository processed, MessageContext context, TimeProvider clock) : IRegistrarOrdemRecebidaUseCase
{
    public async Task<Result<bool>> ExecuteAsync(RegistrarOrdemRecebidaCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);

        if (context.IncomingMessageId is Guid messageId && await processed.ExistsAsync(messageId, cancellationToken))
            return Result<bool>.Success(true);

        var existing = await repository.GetByIdAsync(command.OrdemServicoId, cancellationToken);

        if (existing.IsSuccess)
        {
            var snapshot = existing.Value!.Snapshot();

            if (snapshot.ClienteId != command.ClienteId || snapshot.VeiculoId != command.VeiculoId || snapshot.DescricaoProblema != command.DescricaoProblema?.Trim())
                return Result<bool>.Failure(Error.Conflict("Já existe execução com dados diferentes para esta ordem."));
        }
        else
        {
            if (existing.Error.Code != ErrorCode.NotFound)
                return Result<bool>.Failure(existing.Error);

            var created = Execucao.Create(command.OrdemServicoId, command.ClienteId, command.VeiculoId, command.DescricaoProblema,
                context.IncomingOccurredAtUtc ?? clock.GetUtcNow());

            if (!created.IsSuccess)
                return Result<bool>.Failure(created.Error);

            await repository.AddAsync(created.Value!, cancellationToken);
        }

        if (context.IncomingMessageId is Guid incoming)
            await processed.AddAsync(incoming, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result<bool>.Success(true);
    }
}
