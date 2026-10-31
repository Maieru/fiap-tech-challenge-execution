using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ListarServicos;

public sealed class ListarServicosUseCase(IServicoRepository servicoRepository) : IListarServicosUseCase
{
    private readonly IServicoRepository _servicoRepository = servicoRepository;

    public async Task<Result<ListarServicosResponse>> ExecuteAsync(ListarServicosCommand command, CancellationToken cancellationToken = default)
    {
        var paginationValidationResult = ValidatePagination(command.PageNumber, command.PageSize);

        if (!paginationValidationResult.IsSuccess)
            return Result<ListarServicosResponse>.Failure(paginationValidationResult.Error);

        var pagedResult = await _servicoRepository.GetPagedAsync(command.PageNumber, command.PageSize, cancellationToken);

        if (!pagedResult.IsSuccess)
            return Result<ListarServicosResponse>.Failure(pagedResult.Error);

        return Result<ListarServicosResponse>.Success(new ListarServicosResponse
        {
            PageNumber = command.PageNumber,
            PageSize = command.PageSize,
            TotalItems = pagedResult.Value.TotalItems,
            Servicos = pagedResult.Value.Servicos.Select(ToItemResponse).ToArray()
        });
    }

    private static Result<bool> ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            return Result<bool>.Failure(new Error("O numero da pagina deve ser maior que zero."));

        if (pageSize <= 0)
            return Result<bool>.Failure(new Error("O tamanho da pagina deve ser maior que zero."));

        return Result<bool>.Success(true);
    }

    private static ListarServicoItemResponse ToItemResponse(Servico servico)
    {
        return new ListarServicoItemResponse
        {
            Id = servico.Id,
            Descricao = servico.Descricao,
            ValorUnitario = servico.ValorUnitario
        };
    }
}
