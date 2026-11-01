using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class ServicoRepository(MongoStore store) : IServicoRepository, IServicoDaOrdemDeServicoRepository
{
    private readonly Dictionary<Guid, long> versions = [];
    private IMongoCollection<ServicoDocument> Collection => store.Collection<ServicoDocument>("servicos");

    public async Task<Result<Servico>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await store.Find(Collection, Builders<ServicoDocument>.Filter.Where(x => x.Id == id && x.Ativo))
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null) return Result<Servico>.Failure(Error.NotFound("Serviço"));
        versions[id] = row.Version;
        return Servico.Rehydrate(row.Id, row.Descricao, row.ValorUnitario);
    }

    public async Task<Result<(IReadOnlyCollection<Servico> Servicos, int TotalItems)>> GetPagedAsync(
        int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = store.Find(Collection, Builders<ServicoDocument>.Filter.Eq(x => x.Ativo, true));
        var total = await query.CountDocumentsAsync(cancellationToken);
        var rows = await query.SortBy(x => x.Descricao).ThenBy(x => x.Id)
            .Skip(checked((pageNumber - 1) * pageSize)).Limit(pageSize).ToListAsync(cancellationToken);
        IReadOnlyCollection<Servico> items = rows.Select(x => Servico.Rehydrate(x.Id, x.Descricao, x.ValorUnitario).Value!).ToArray();
        return Result<(IReadOnlyCollection<Servico>, int)>.Success((items, checked((int)total)));
    }

    public Task AddAsync(Servico service, CancellationToken cancellationToken = default) =>
        store.InsertAsync(Collection, new ServicoDocument { Id = service.Id, Descricao = service.Descricao,
            ValorUnitario = service.ValorUnitario, Ativo = true }, cancellationToken);

    public Task UpdateAsync(Servico service, CancellationToken cancellationToken = default) => SaveAsync(service, true, cancellationToken);
    public Task DeleteAsync(Servico service, CancellationToken cancellationToken = default) => SaveAsync(service, false, cancellationToken);

    private async Task SaveAsync(Servico service, bool active, CancellationToken token)
    {
        var version = versions[service.Id];
        var filter = Builders<ServicoDocument>.Filter.Where(x => x.Id == service.Id && x.Version == version && x.Ativo);
        var result = await store.ReplaceAsync(Collection, filter, new ServicoDocument
        {
            Id = service.Id, Descricao = service.Descricao, ValorUnitario = service.ValorUnitario, Ativo = active, Version = version + 1
        }, token);
        if (result.MatchedCount != 1) throw new ConcurrencyException();
        versions[service.Id] = version + 1;
    }

    public async Task<Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>> GetConcluidosByServicoIdAsync(
        Guid servicoId, CancellationToken cancellationToken = default)
    {
        var collection = store.Collection<ExecucaoDocument>("execucoes");
        var rows = await store.Find(collection, Builders<ExecucaoDocument>.Filter
            .Where(x => x.Snapshot.Servicos.Any(s => s.ServicoId == servicoId && s.Concluido))).ToListAsync(cancellationToken);
        IReadOnlyCollection<ServicoDaOrdemDeServico> services = rows.SelectMany(x => x.Snapshot.Servicos)
            .Where(x => x.ServicoId == servicoId && x.Concluido).Select(x => ServicoDaOrdemDeServico.Rehydrate(x).Value!).ToArray();
        return Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>.Success(services);
    }
}
