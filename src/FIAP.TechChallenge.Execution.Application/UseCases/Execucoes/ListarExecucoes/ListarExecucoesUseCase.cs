using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.ListarExecucoes;

public sealed record ListarExecucoesCommand(StatusExecucao? Status = null, int PageNumber = 1, int PageSize = 10);
public sealed record ListarExecucoesResponse(IReadOnlyList<ExecucaoResponse> Items, long TotalItems, int PageNumber, int PageSize);
public interface IListarExecucoesUseCase
{
    Task<Result<ListarExecucoesResponse>> ExecuteAsync(ListarExecucoesCommand command, CancellationToken cancellationToken = default);
}
public sealed class ListarExecucoesUseCase(IExecucaoRepository repository) : IListarExecucoesUseCase
{
    public async Task<Result<ListarExecucoesResponse>> ExecuteAsync(ListarExecucoesCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PageNumber < 1 || command.PageSize is < 1 or > 100
            || (long)(command.PageNumber - 1) * command.PageSize > int.MaxValue
            || (command.Status.HasValue && !Enum.IsDefined(command.Status.Value)))
            return Result<ListarExecucoesResponse>.Failure(new Error("Paginação ou status inválido. PageSize deve estar entre 1 e 100."));

        var result = await repository.GetPagedAsync(command.Status, command.PageNumber, command.PageSize, cancellationToken);

        return Result<ListarExecucoesResponse>.Success(new(result.Items.Select(ExecucaoResponse.From).ToArray(),
            result.Total, command.PageNumber, command.PageSize));
    }
}
