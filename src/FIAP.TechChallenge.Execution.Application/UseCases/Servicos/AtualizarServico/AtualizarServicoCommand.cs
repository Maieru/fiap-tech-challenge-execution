namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.AtualizarServico;

public sealed class AtualizarServicoCommand
{
    public Guid Id { get; init; }
    public string Descricao { get; init; } = string.Empty;
    public decimal ValorUnitario { get; init; }
}
