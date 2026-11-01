namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;

public sealed class ListarPecasInsumosResponse
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public IReadOnlyCollection<ListarPecaInsumoItemResponse> PecasInsumos { get; init; } = [];
}

public sealed class ListarPecaInsumoItemResponse
{
    public Guid Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public string Codigo { get; init; } = string.Empty;
    public string? Descricao { get; init; }
    public decimal PrecoUnitario { get; init; }
    public int QuantidadeEstoque { get; init; }
    public bool Ativo { get; init; }
}

