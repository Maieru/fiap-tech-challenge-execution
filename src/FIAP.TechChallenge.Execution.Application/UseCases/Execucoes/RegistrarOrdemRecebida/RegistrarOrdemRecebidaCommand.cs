namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RegistrarOrdemRecebida;

public sealed record RegistrarOrdemRecebidaCommand(Guid OrdemServicoId, Guid ClienteId, Guid VeiculoId, string DescricaoProblema);
