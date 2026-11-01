namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;

public sealed class EntradaEstoquePecaInsumoCommand
{
    public Guid Id { get; init; }
    public int Quantidade { get; init; }
}

