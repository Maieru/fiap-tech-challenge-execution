namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.VerificarTempoMedioServico;

public sealed class VerificarTempoMedioServicoResponse
{
    public Guid ServicoId { get; init; }
    public decimal TempoMedioMinutos { get; init; }
    public int QuantidadeExecucoes { get; init; }
}
