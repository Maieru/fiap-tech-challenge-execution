using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class PecaInsumoRepository(MongoStore store) : IPecaInsumoRepository
{
    private readonly Dictionary<Guid, long> versions = [];
    private IMongoCollection<PecaDocument> Collection => store.Collection<PecaDocument>("pecas_insumos");

    public async Task<bool> ExistsByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        await store.Find(Collection, Builders<PecaDocument>.Filter.Where(x => x.Codigo == codigo && x.Ativo))
            .AnyAsync(cancellationToken);

    public Task<Result<PecaInsumo>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ReadAsync(Builders<PecaDocument>.Filter.Where(x => x.Id == id && x.Ativo), cancellationToken);
    public Task<Result<PecaInsumo>> GetIncludingInactiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ReadAsync(Builders<PecaDocument>.Filter.Eq(x => x.Id, id), cancellationToken);
    public Task<Result<PecaInsumo>> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default) =>
        ReadAsync(Builders<PecaDocument>.Filter.Where(x => x.Codigo == codigo && x.Ativo), cancellationToken);

    private async Task<Result<PecaInsumo>> ReadAsync(FilterDefinition<PecaDocument> filter, CancellationToken token)
    {
        var row = await store.Find(Collection, filter).SingleOrDefaultAsync(token);
        if (row is null) return Result<PecaInsumo>.Failure(Error.NotFound("Peça ou insumo"));
        versions[row.Id] = row.Version;
        return Map(row);
    }

    public async Task<Result<(IReadOnlyCollection<PecaInsumo> PecasInsumos, int TotalItems)>> GetPagedAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = store.Find(Collection, Builders<PecaDocument>.Filter.Eq(x => x.Ativo, true));
        var count = await query.CountDocumentsAsync(cancellationToken);
        var rows = await query.SortBy(x => x.Nome).ThenBy(x => x.Id)
            .Skip(checked((pageNumber - 1) * pageSize)).Limit(pageSize).ToListAsync(cancellationToken);
        IReadOnlyCollection<PecaInsumo> items = rows.Select(x => Map(x).Value!).ToArray();
        return Result<(IReadOnlyCollection<PecaInsumo>, int)>.Success((items, checked((int)count)));
    }

    public Task AddAsync(PecaInsumo part, CancellationToken cancellationToken = default) =>
        store.InsertAsync(Collection, Document(part, 0), cancellationToken);

    public async Task UpdateAsync(PecaInsumo part, CancellationToken cancellationToken = default)
    {
        var version = versions[part.Id];
        var result = await store.ReplaceAsync(Collection, Builders<PecaDocument>.Filter.Where(x => x.Id == part.Id && x.Version == version),
            Document(part, version + 1), cancellationToken);
        if (result.MatchedCount != 1) throw new ConcurrencyException();
        versions[part.Id] = version + 1;
    }

    public async Task DeleteAsync(PecaInsumo part, CancellationToken cancellationToken = default)
    {
        _ = part.Inactivate();
        await UpdateAsync(part, cancellationToken);
    }

    private static Result<PecaInsumo> Map(PecaDocument row) =>
        PecaInsumo.Rehydrate(row.Id, row.Nome, row.Codigo, row.Descricao, row.PrecoUnitario, row.QuantidadeEstoque, row.Ativo);
    private static PecaDocument Document(PecaInsumo part, long version) => new()
    {
        Id = part.Id, Nome = part.Nome, Codigo = part.Codigo, Descricao = part.Descricao,
        PrecoUnitario = part.PrecoUnitario, QuantidadeEstoque = part.QuantidadeEstoque, Ativo = part.Ativo, Version = version
    };
}
