using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes;

public sealed record ExecucaoResponse(ExecucaoSnapshot Execucao, decimal ValorTotal)
{
    public static ExecucaoResponse From(Execucao execucao) => new(execucao.Snapshot(), execucao.ValorTotal);
}
