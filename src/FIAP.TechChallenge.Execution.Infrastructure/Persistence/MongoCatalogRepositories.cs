using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoServicoRepository(IMongoDatabase database, MongoSession session) : IServicoRepository
{
    private readonly IMongoCollection<ServicoDocument> _items = database.GetCollection<ServicoDocument>("servicos");
    public async Task<Result<Servico>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = (await session.ReadAsync(_items, Builders<ServicoDocument>.Filter.Where(x => x.Id == id && x.Ativo), cancellationToken)).SingleOrDefault();
        if (row is null) return Result<Servico>.Failure(new Error("Servico nao encontrado.", ErrorCode.NotFound));
        session.Versions["servico:" + id] = row.Version;
        return Servico.Rehydrate(row.Id, row.Descricao, row.ValorUnitario);
    }
    public async Task<Result<(IReadOnlyCollection<Servico> Servicos, int TotalItems)>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var rows = await session.ReadPageAsync(_items, Builders<ServicoDocument>.Filter.Eq(x => x.Ativo, true),
            Builders<ServicoDocument>.Sort.Ascending(x => x.Id), pageNumber, pageSize, cancellationToken);
        return Result<(IReadOnlyCollection<Servico>, int)>.Success((rows.Items
            .Select(x => Servico.Rehydrate(x.Id, x.Descricao, x.ValorUnitario).Value!).ToArray(), rows.Total));
    }
    public Task AddAsync(Servico value, CancellationToken cancellationToken = default) => session.InsertAsync(_items,
        new ServicoDocument { Id = value.Id, Descricao = value.Descricao, ValorUnitario = value.ValorUnitario }, cancellationToken);
    public async Task UpdateAsync(Servico value, CancellationToken cancellationToken = default)
    {
        var version = session.Versions["servico:" + value.Id];
        var result = await session.UpdateAsync(_items, Builders<ServicoDocument>.Filter.Where(x => x.Id == value.Id && x.Ativo && x.Version == version),
            Builders<ServicoDocument>.Update.Set(x => x.Descricao, value.Descricao).Set(x => x.ValorUnitario, value.ValorUnitario).Inc(x => x.Version, 1), cancellationToken);
        if (result.ModifiedCount != 1) throw new OperationConflictException("Servico alterado ou inativado por outra operacao.");
    }
    public async Task DeleteAsync(Servico value, CancellationToken cancellationToken = default)
    {
        var version = session.Versions["servico:" + value.Id];
        var result = await session.UpdateAsync(_items, Builders<ServicoDocument>.Filter.Where(x => x.Id == value.Id && x.Ativo && x.Version == version),
            Builders<ServicoDocument>.Update.Set(x => x.Ativo, false).Inc(x => x.Version, 1), cancellationToken);
        if (result.ModifiedCount != 1) throw new OperationConflictException("Servico alterado ou inativado por outra operacao.");
    }
}

public sealed class MongoPecaRepository(IMongoDatabase database, MongoSession session) : IPecaInsumoRepository
{
    private readonly IMongoCollection<PecaDocument> _items = database.GetCollection<PecaDocument>("pecas");
    public async Task<bool> ExistsByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        (await session.ReadAsync(_items, Builders<PecaDocument>.Filter.Where(x => x.Codigo == codigo && x.Ativo), cancellationToken)).Count > 0;
    public Task<Result<PecaInsumo>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Get(Builders<PecaDocument>.Filter.Eq(x => x.Id, id), cancellationToken);
    public Task<Result<PecaInsumo>> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default) => Get(Builders<PecaDocument>.Filter.Eq(x => x.Codigo, codigo), cancellationToken);
    private async Task<Result<PecaInsumo>> Get(FilterDefinition<PecaDocument> filter, CancellationToken ct)
    {
        var row = (await session.ReadAsync(_items, filter & Builders<PecaDocument>.Filter.Eq(x => x.Ativo, true), ct)).SingleOrDefault();
        if (row is null) return Result<PecaInsumo>.Failure(new Error("Peca ou insumo nao encontrado.", ErrorCode.NotFound));
        session.Versions["peca:" + row.Id] = row.Version;
        return Map(row);
    }
    public async Task<Result<(IReadOnlyCollection<PecaInsumo> PecasInsumos, int TotalItems)>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var rows = await session.ReadPageAsync(_items, Builders<PecaDocument>.Filter.Eq(x => x.Ativo, true),
            Builders<PecaDocument>.Sort.Ascending(x => x.Id), pageNumber, pageSize, cancellationToken);
        return Result<(IReadOnlyCollection<PecaInsumo>, int)>.Success((rows.Items.Select(x => Map(x).Value!).ToArray(), rows.Total));
    }
    public Task AddAsync(PecaInsumo value, CancellationToken cancellationToken = default) => session.InsertAsync(_items, From(value), cancellationToken);
    public async Task UpdateAsync(PecaInsumo value, CancellationToken cancellationToken = default)
    {
        var version = session.Versions["peca:" + value.Id];
        var row = From(value); row.Version = version + 1;
        var result = await session.ReplaceAsync(_items, Builders<PecaDocument>.Filter.Where(x => x.Id == value.Id && x.Ativo && x.Version == version), row, cancellationToken);
        if (result.ModifiedCount != 1) throw new OperationConflictException("Peca alterada ou inativada por outra operacao.");
    }
    public async Task DeleteAsync(PecaInsumo value, CancellationToken cancellationToken = default)
    {
        var version = session.Versions["peca:" + value.Id];
        var result = await session.UpdateAsync(_items, Builders<PecaDocument>.Filter.Where(x => x.Id == value.Id && x.Ativo && x.Version == version),
            Builders<PecaDocument>.Update.Set(x => x.Ativo, false).Inc(x => x.Version, 1), cancellationToken);
        if (result.ModifiedCount != 1) throw new OperationConflictException("Peca alterada ou inativada por outra operacao.");
    }
    private static Result<PecaInsumo> Map(PecaDocument x) => PecaInsumo.Rehydrate(x.Id, x.Nome, x.Codigo, x.Descricao, x.PrecoUnitario, x.QuantidadeEstoque, x.Ativo);
    private static PecaDocument From(PecaInsumo x) => new() { Id = x.Id, Nome = x.Nome, Codigo = x.Codigo, Descricao = x.Descricao,
        PrecoUnitario = x.PrecoUnitario, QuantidadeEstoque = x.QuantidadeEstoque, Ativo = x.Ativo };
}
