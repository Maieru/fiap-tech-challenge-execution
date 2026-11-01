namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ListarServicos;

public sealed class ListarServicosResponse
{
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public IReadOnlyCollection<ListarServicoItemResponse> Servicos { get; init; } = [];
}

public sealed class ListarServicoItemResponse
{
    public Guid Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public decimal ValorUnitario { get; init; }
}

