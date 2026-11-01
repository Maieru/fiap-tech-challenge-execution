namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.RecuperarServico;

public sealed class RecuperarServicoResponse
{
    public Guid Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public decimal ValorUnitario { get; init; }
}

