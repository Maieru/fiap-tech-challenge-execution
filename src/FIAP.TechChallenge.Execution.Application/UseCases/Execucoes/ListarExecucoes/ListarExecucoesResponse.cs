namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ListarExecucoes;

public sealed record ListarExecucoesResponse(IReadOnlyList<ExecucaoResponse> Items, long TotalItems, int PageNumber, int PageSize);
