namespace FIAP.TechChallenge.Execution.Application.Messaging;

public sealed record DiagnosticoIniciado(Guid OrdemServicoId)
{
    public const string Type = "execution.diagnostico-iniciado";
}
