namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;

public sealed class ListarPecasInsumosCommand
{
    public string? Codigo { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}

