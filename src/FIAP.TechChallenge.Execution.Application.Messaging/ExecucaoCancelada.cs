namespace FIAP.TechChallenge.Execution.Application.Messaging;

public sealed record ExecucaoCancelada(Guid OrdemServicoId)
{
    public const string Type = "execution.execucao-cancelada";
}
