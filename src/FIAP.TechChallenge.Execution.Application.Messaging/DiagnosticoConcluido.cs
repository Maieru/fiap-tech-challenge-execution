namespace FIAP.TechChallenge.Execution.Application.Messaging;

public sealed record DiagnosticoConcluido(
    Guid OrdemServicoId,
    IReadOnlyList<ServicoDiagnostico> Servicos,
    IReadOnlyList<PecaInsumoDiagnostico> PecasInsumos,
    decimal ValorTotal)
{
    public const string Type = "execution.diagnostico-concluido";
}

public sealed record ServicoDiagnostico(
    Guid ItemId, Guid ServicoId, string Descricao,
    decimal ValorUnitario, int Quantidade, decimal ValorTotal);

public sealed record PecaInsumoDiagnostico(
    Guid ItemId, Guid PecaInsumoId, string Nome, string Codigo, string? Descricao,
    decimal PrecoUnitario, int Quantidade, decimal ValorTotal);
