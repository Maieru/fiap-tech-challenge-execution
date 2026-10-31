namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.CadastrarServico;

public sealed class CadastrarServicoResponse
{
    public Guid Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public decimal ValorUnitario { get; init; }
}
