namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ConcluirServico;

public sealed record ConcluirServicoCommand(Guid OrdemServicoId, Guid ItemId, int TempoGastoMinutos);
