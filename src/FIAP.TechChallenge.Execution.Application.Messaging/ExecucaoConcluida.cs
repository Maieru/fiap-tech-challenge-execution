namespace FIAP.TechChallenge.Execution.Application.Messaging;

public sealed record ExecucaoConcluida(Guid OrdemServicoId)
{
    public const string Type = "execution.execucao-concluida";
}
