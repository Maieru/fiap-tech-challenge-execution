namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.AdicionarPecaInsumo;

public sealed record AdicionarPecaInsumoCommand(Guid OrdemServicoId, Guid PecaInsumoId, int Quantidade);
