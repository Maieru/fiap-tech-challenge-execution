namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.IncluirPecaInsumo;

public sealed class IncluirPecaInsumoCommand
{
    public string Nome { get; init; } = string.Empty;
    public string Codigo { get; init; } = string.Empty;
    public string? Descricao { get; init; }
    public decimal PrecoUnitario { get; init; }
    public int QuantidadeEstoque { get; init; }
}
