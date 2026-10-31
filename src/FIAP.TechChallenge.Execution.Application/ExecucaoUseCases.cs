using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application;

public sealed record ItemCommand(Guid ItemId, int Quantidade);
public sealed record ConcluirServicoCommand(int TempoGastoMinutos);
public sealed record PecaAdicionadaResponse(Guid Id, Guid OrdemServicoId, Guid PecaInsumoId, string Nome, string Codigo,
    string? Descricao, decimal PrecoUnitario, int Quantidade)
{
    public decimal ValorTotal => PrecoUnitario * Quantidade;
}
public sealed record FilaResponse(IReadOnlyCollection<Execucao> Items, int TotalItems, int PageNumber, int PageSize);
public sealed class ExecucaoUseCases(IExecutionRepository repository, IServicoRepository servicos, IPecaInsumoRepository pecas,
    IReservations reservations, ITransactionRunner transactions, IIntegrationEvents events, IdempotentCommandRunner idempotency, TimeProvider clock)
{
    public async Task<Result<Execucao>> ConsultarAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) is { } value ? Result<Execucao>.Success(value) : Missing<Execucao>();

    public async Task<Result<FilaResponse>> ListarAsync(EstadoExecucao? estado, int page, int size, CancellationToken ct)
    {
        if (page < 1 || size < 1 || size > 100 || (estado.HasValue && !Enum.IsDefined(estado.Value)))
            return Result<FilaResponse>.Failure(new Error("Paginacao ou estado invalido."));
        var result = await repository.ListAsync(estado, page, size, ct);
        return Result<FilaResponse>.Success(new(result.Items, result.Total, page, size));
    }
    public Task<Result<Execucao>> IniciarDiagnosticoAsync(Guid id, CancellationToken ct) => Change(id, x => x.IniciarDiagnostico(Now), "DiagnosticoIniciado", ct);
    public Task<Result<Execucao>> ConcluirDiagnosticoAsync(Guid id, CancellationToken ct) => Change(id, x => x.ConcluirDiagnostico(Now), "DiagnosticoConcluido", ct);
    public Task<Result<Execucao>> FinalizarAsync(Guid id, CancellationToken ct) => Change(id, x => x.Finalizar(Now), "ExecucaoFinalizada", ct);
    public Task<Result<Execucao>> ConcluirServicoAsync(Guid id, Guid itemId, int minutos, CancellationToken ct) => Change(id, x => x.ConcluirServico(itemId, minutos), null, ct);

    public Task<Result<ServicoDaOrdemDeServico>> AdicionarServicoAsync(Guid id, ItemCommand command, CancellationToken ct) =>
        transactions.ExecuteAsync<ServicoDaOrdemDeServico>(async token =>
        {
            var entity = await repository.GetAsync(id, token);
            if (entity is null) return Missing<ServicoDaOrdemDeServico>();
            var catalog = await servicos.GetByIdAsync(command.ItemId, token);
            if (!catalog.IsSuccess) return Result<ServicoDaOrdemDeServico>.Failure(catalog.Error);
            var version = entity.Version;
            var result = entity.AdicionarServico(catalog.Value!, command.Quantidade);
            if (result.IsSuccess) await repository.SaveAsync(entity, version, token);
            return result;
        }, ct);

    public Task<Result<PecaAdicionadaResponse>> AdicionarPecaAsync(Guid id, ItemCommand command, string? key, CancellationToken ct) =>
        idempotency.ExecuteAsync<PecaAdicionadaResponse>(key, $"ordens/{id}/pecas", command, async token =>
        {
            var entity = await repository.GetAsync(id, token);
            if (entity is null) return Missing<PecaAdicionadaResponse>();
            var catalog = await pecas.GetByIdAsync(command.ItemId, token);
            if (!catalog.IsSuccess) return Result<PecaAdicionadaResponse>.Failure(catalog.Error);
            var version = entity.Version;
            var result = entity.AdicionarPeca(catalog.Value!, command.Quantidade);
            if (!result.IsSuccess) return Result<PecaAdicionadaResponse>.Failure(result.Error);
            var stock = catalog.Value!.RemoveEstoque(command.Quantidade);
            if (!stock.IsSuccess) return Result<PecaAdicionadaResponse>.Failure(new Error(stock.Error.Description, ErrorCode.Conflict));
            await pecas.UpdateAsync(catalog.Value, token);
            await reservations.AddAsync(id, result.Value!.Id, catalog.Value.Id, command.Quantidade, token);
            await repository.SaveAsync(entity, version, token);
            var item = result.Value!;
            return Result<PecaAdicionadaResponse>.Success(new(item.Id, item.OrdemServicoId, item.PecaInsumoId, item.Nome, item.Codigo,
                item.Descricao, item.PrecoUnitario, item.Quantidade));
        }, ct);

    private Task<Result<Execucao>> Change(Guid id, Func<Execucao, Result<bool>> change, string? eventType, CancellationToken ct) =>
        transactions.ExecuteAsync<Execucao>(async token =>
        {
            var entity = await repository.GetAsync(id, token);
            if (entity is null) return Missing<Execucao>();
            var version = entity.Version;
            var result = change(entity);
            if (!result.IsSuccess) return Result<Execucao>.Failure(result.Error);
            await repository.SaveAsync(entity, version, token);
            if (eventType is not null) await events.EnqueueAsync(eventType, id, entity.StreamSequence,
                eventType == "DiagnosticoConcluido" ? entity : null, null, null, token);
            return Result<Execucao>.Success(entity);
        }, ct);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static Result<T> Missing<T>() => Result<T>.Failure(new Error("Ordem de servico nao encontrada.", ErrorCode.NotFound));
}
