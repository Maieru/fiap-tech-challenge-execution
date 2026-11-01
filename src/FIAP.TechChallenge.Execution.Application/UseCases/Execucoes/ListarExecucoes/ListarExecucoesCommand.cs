using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ListarExecucoes;

public sealed record ListarExecucoesCommand(StatusExecucao? Status = null, int PageNumber = 1, int PageSize = 10);
