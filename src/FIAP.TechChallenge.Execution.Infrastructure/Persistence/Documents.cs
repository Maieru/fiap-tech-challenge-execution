using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class ServicoDocument
{
    public Guid Id { get; set; }
    public string Descricao { get; set; } = "";
    public decimal ValorUnitario { get; set; }
    public bool Ativo { get; set; } = true;
    public long Version { get; set; }
}
public sealed class PecaDocument
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string? Descricao { get; set; }
    public decimal PrecoUnitario { get; set; }
    public int QuantidadeEstoque { get; set; }
    public bool Ativo { get; set; }
    public long Version { get; set; }
}
public sealed class ExecucaoDocument
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public StatusExecucao Status { get; set; }
    public ExecucaoSnapshot Snapshot { get; set; } = null!;
    public long Version { get; set; }
}
public sealed class ProcessedMessageDocument
{
    public string Id { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}
