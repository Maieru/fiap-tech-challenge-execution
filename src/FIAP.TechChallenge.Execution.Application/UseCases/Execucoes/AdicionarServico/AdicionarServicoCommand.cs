namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarServico;

public sealed record AdicionarServicoCommand(Guid OrdemServicoId, Guid ServicoId, int Quantidade);
