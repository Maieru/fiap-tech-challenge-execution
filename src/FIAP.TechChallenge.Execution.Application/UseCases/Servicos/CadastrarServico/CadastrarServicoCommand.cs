namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.CadastrarServico;

public sealed class CadastrarServicoCommand
{
    public string Descricao { get; init; } = string.Empty;
    public decimal ValorUnitario { get; init; }
}

