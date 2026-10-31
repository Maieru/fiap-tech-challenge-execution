using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoExecutionRepository(IMongoDatabase database, MongoSession session) : IExecutionRepository, IServicoDaOrdemDeServicoRepository
{
    private readonly IMongoCollection<ExecucaoDocument> _items = database.GetCollection<ExecucaoDocument>("execucoes");
    public async Task<Execucao?> GetAsync(Guid ordemId, CancellationToken ct)
    {
        var item = (await session.ReadAsync(_items, Builders<ExecucaoDocument>.Filter.Eq(x => x.OrdemServicoId, ordemId), ct)).SingleOrDefault();
        return item is null ? null : Execucao.Rehydrate(item.Snapshot);
    }
    public async Task<(IReadOnlyCollection<Execucao> Items, int Total)> ListAsync(EstadoExecucao? estado, int page, int size, CancellationToken ct)
    {
        var filter = estado.HasValue ? Builders<ExecucaoDocument>.Filter.Eq(x => x.Estado, estado) : Builders<ExecucaoDocument>.Filter.Empty;
        var rows = await session.ReadPageAsync(_items, filter,
            Builders<ExecucaoDocument>.Sort.Ascending(x => x.DataCriacao).Ascending(x => x.OrdemServicoId), page, size, ct);
        return (rows.Items.Select(x => Execucao.Rehydrate(x.Snapshot)).ToArray(), rows.Total);
    }
    public Task AddAsync(Execucao entity, CancellationToken ct) => session.InsertAsync(_items, ExecucaoDocument.From(entity), ct);
    public async Task SaveAsync(Execucao entity, long expectedVersion, CancellationToken ct)
    {
        var result = await session.ReplaceAsync(_items, Builders<ExecucaoDocument>.Filter.Where(x => x.OrdemServicoId == entity.OrdemServicoId && x.Version == expectedVersion), ExecucaoDocument.From(entity), ct);
        if (result.ModifiedCount != 1) throw new OperationConflictException("A execucao foi alterada por outra operacao.");
    }
    public async Task<Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>> GetConcluidosByServicoIdAsync(Guid servicoId, CancellationToken cancellationToken = default)
    {
        var rows = await session.ReadAsync(_items, Builders<ExecucaoDocument>.Filter.Where(x => x.Snapshot.Servicos.Any(s => s.ServicoId == servicoId && s.Concluido)), cancellationToken);
        return Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>.Success(rows.SelectMany(x => x.Snapshot.Servicos)
            .Where(x => x.ServicoId == servicoId && x.Concluido).Select(x => ServicoDaOrdemDeServico.Rehydrate(x).Value!).ToArray());
    }
}

public sealed class MongoCommandStore(IMongoDatabase database, MongoSession session) : ICommandStore
{
    private readonly IMongoCollection<CommandDocument> _items = database.GetCollection<CommandDocument>("commands");
    public async Task<StoredCommand?> GetAsync(string id, CancellationToken ct)
    {
        var row = (await session.ReadAsync(_items, Builders<CommandDocument>.Filter.Eq(x => x.Id, id), ct)).SingleOrDefault();
        return row is null ? null : new StoredCommand(row.RequestHash, row.ResponseJson);
    }
    public Task AddAsync(string id, StoredCommand value, CancellationToken ct) => session.InsertAsync(_items,
        new CommandDocument { Id = id, RequestHash = value.RequestHash, ResponseJson = value.ResponseJson }, ct);
}

public sealed class MongoReservations(IMongoDatabase database, MongoSession session) : IReservations
{
    private readonly IMongoCollection<ReservaDocument> _items = database.GetCollection<ReservaDocument>("reservas");
    public Task AddAsync(Guid ordemId, Guid itemId, Guid pecaId, int quantidade, CancellationToken ct) => session.InsertAsync(_items,
        new ReservaDocument { Id = itemId, OrdemServicoId = ordemId, PecaId = pecaId, Quantidade = quantidade }, ct);
    public async Task ReleaseAsync(Guid ordemId, CancellationToken ct)
    {
        var rows = await session.ReadAsync(_items, Builders<ReservaDocument>.Filter.Where(x => x.OrdemServicoId == ordemId && !x.Devolvida), ct);
        foreach (var row in rows)
        {
            var restored = await session.UpdateAsync(database.GetCollection<PecaDocument>("pecas"), Builders<PecaDocument>.Filter.Eq(x => x.Id, row.PecaId),
                Builders<PecaDocument>.Update.Inc(x => x.QuantidadeEstoque, row.Quantidade).Inc(x => x.Version, 1), ct);
            if (restored.ModifiedCount != 1) throw new InvalidOperationException("Peca da reserva nao encontrada para compensacao.");
            await session.UpdateAsync(_items, Builders<ReservaDocument>.Filter.Where(x => x.Id == row.Id && !x.Devolvida), Builders<ReservaDocument>.Update.Set(x => x.Devolvida, true), ct);
        }
    }
}
