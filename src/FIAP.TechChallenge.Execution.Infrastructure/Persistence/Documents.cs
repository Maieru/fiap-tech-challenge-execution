using FIAP.TechChallenge.Execution.Domain.Entities;
using MongoDB.Bson.Serialization.Attributes;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class ServicoDocument
{
    [BsonId] public Guid Id { get; set; }
    public string Descricao { get; set; } = "";
    public decimal ValorUnitario { get; set; }
    public bool Ativo { get; set; } = true;
    public long Version { get; set; }
}
public sealed class PecaDocument
{
    [BsonId] public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string? Descricao { get; set; }
    public decimal PrecoUnitario { get; set; }
    public int QuantidadeEstoque { get; set; }
    public bool Ativo { get; set; } = true;
    public long Version { get; set; }
}
public sealed class ExecucaoDocument
{
    [BsonId] public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public EstadoExecucao Estado { get; set; }
    public DateTime DataCriacao { get; set; }
    public long Version { get; set; }
    public ExecucaoSnapshot Snapshot { get; set; } = null!;
    public static ExecucaoDocument From(Execucao entity) => new() { Id = entity.Id, OrdemServicoId = entity.OrdemServicoId,
        Estado = entity.Estado, DataCriacao = entity.DataCriacao, Version = entity.Version, Snapshot = entity.Snapshot() };
}
public sealed class ReservaDocument
{
    [BsonId] public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public Guid PecaId { get; set; }
    public int Quantidade { get; set; }
    public bool Devolvida { get; set; }
}
public sealed class CommandDocument
{
    [BsonId] public string Id { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResponseJson { get; set; } = "";
}
public sealed class InboxDocument
{
    [BsonId] public Guid Id { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public string? Body { get; set; }
}
public sealed class OutboxDocument
{
    [BsonId] public Guid Id { get; set; }
    public Guid OrdemServicoId { get; set; }
    public long Sequence { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public bool Published { get; set; }
}
public sealed class DispatcherLease
{
    [BsonId] public string Id { get; set; } = "dispatcher";
    public string Owner { get; set; } = "";
    public DateTime UntilUtc { get; set; }
}
