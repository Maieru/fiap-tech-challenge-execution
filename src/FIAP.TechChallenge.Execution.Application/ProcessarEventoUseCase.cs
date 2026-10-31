using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application;

public sealed record IncomingEvent(Guid MessageId, string Type, Guid OrdemServicoId, long Sequence, DateTime OccurredAtUtc,
    Guid? ClienteId = null, Guid? VeiculoId = null, string? DescricaoProblema = null, Guid? SolicitacaoId = null, string? OperadorId = null);

public sealed class ProcessarEventoUseCase(IExecutionRepository repository, ITransactionRunner transactions, IInbox inbox,
    IReservations reservations, IIntegrationEvents events)
{
    public async Task<bool> ExecuteAsync(IncomingEvent message, CancellationToken ct)
    {
        var compensated = false;
        var result = await transactions.ExecuteAsync<bool>(async token =>
        {
            compensated = false;
            if (await inbox.ContainsAsync(message.MessageId, token)) return Result<bool>.Success(true);
            var entity = await repository.GetAsync(message.OrdemServicoId, token);
            if (message.Type == "OrdemCriada")
            {
                if (message.Sequence != 1) throw new InvalidOperationException("OrdemCriada deve iniciar a sequencia.");
                if (entity is null)
                {
                    var created = Execucao.Create(message.OrdemServicoId, message.ClienteId ?? Guid.Empty, message.VeiculoId ?? Guid.Empty,
                        message.DescricaoProblema ?? "", message.OccurredAtUtc);
                    if (!created.IsSuccess) throw new InvalidOperationException(created.Error.Description);
                    await repository.AddAsync(created.Value!, token);
                }
            }
            else
            {
                if (entity is null) throw new InvalidOperationException("OrdemCriada ainda nao foi aplicada.");
                if (message.Sequence <= entity.InputSequence) { await inbox.AddAsync(message.MessageId, token); return Result<bool>.Success(true); }
                if (message.SolicitacaoId is null || string.IsNullOrWhiteSpace(message.OperadorId)) throw new InvalidOperationException("Solicitacao sem identificacao ou operador.");
                var version = entity.Version;
                entity.AcceptInput(message.Sequence);
                var transition = message.Type switch
                {
                    "AutorizacaoExecucaoSolicitada" => entity.Autorizar(message.OccurredAtUtc),
                    "CancelamentoSolicitado" => entity.Cancelar(),
                    _ => throw new InvalidOperationException("Tipo de evento nao suportado.")
                };
                var type = transition.IsSuccess ? message.Type == "CancelamentoSolicitado" ? "ExecucaoCancelada" : "ExecucaoIniciada" : "SolicitacaoRecusada";
                if (!transition.IsSuccess) entity.RejectInput();
                if (transition.IsSuccess && type == "ExecucaoCancelada")
                {
                    await reservations.ReleaseAsync(message.OrdemServicoId, token);
                    compensated = true;
                }
                await repository.SaveAsync(entity, version, token);
                await events.EnqueueAsync(type, entity.OrdemServicoId, entity.StreamSequence, null, message.SolicitacaoId,
                    transition.IsSuccess ? null : transition.Error.Description, token);
            }
            await inbox.AddAsync(message.MessageId, token);
            return Result<bool>.Success(true);
        }, ct);
        if (!result.IsSuccess) throw new InvalidOperationException(result.Error.Description);
        return compensated;
    }
}
