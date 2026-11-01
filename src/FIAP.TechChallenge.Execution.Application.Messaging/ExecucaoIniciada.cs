namespace FIAP.TechChallenge.Execution.Application.Messaging;

public sealed record ExecucaoIniciada(Guid OrdemServicoId)
{
    public const string Type = "execution.execucao-iniciada";
}
