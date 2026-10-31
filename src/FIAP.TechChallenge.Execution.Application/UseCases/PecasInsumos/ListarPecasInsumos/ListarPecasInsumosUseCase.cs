using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;

public sealed class ListarPecasInsumosUseCase(IPecaInsumoRepository pecaInsumoRepository) : IListarPecasInsumosUseCase
{
    private readonly IPecaInsumoRepository _pecaInsumoRepository = pecaInsumoRepository;

    public async Task<Result<ListarPecasInsumosResponse>> ExecuteAsync(ListarPecasInsumosCommand command, CancellationToken cancellationToken = default)
    {
        var paginationValidationResult = ValidatePagination(command.PageNumber, command.PageSize);

        if (!paginationValidationResult.IsSuccess)
            return Result<ListarPecasInsumosResponse>.Failure(paginationValidationResult.Error);

        if (!string.IsNullOrWhiteSpace(command.Codigo))
            return await GetByCodigoAsync(command.Codigo, cancellationToken);

        var pagedResult = await _pecaInsumoRepository.GetPagedAsync(command.PageNumber, command.PageSize, cancellationToken);

        if (!pagedResult.IsSuccess)
            return Result<ListarPecasInsumosResponse>.Failure(pagedResult.Error);

        return Result<ListarPecasInsumosResponse>.Success(new ListarPecasInsumosResponse
        {
            PageNumber = command.PageNumber,
            PageSize = command.PageSize,
            TotalItems = pagedResult.Value.TotalItems,
            PecasInsumos = pagedResult.Value.PecasInsumos.Select(ToItemResponse).ToArray()
        });
    }

    private async Task<Result<ListarPecasInsumosResponse>> GetByCodigoAsync(string codigo, CancellationToken cancellationToken)
    {
        var codigoValidationResult = ValidateCodigo(codigo);

        if (!codigoValidationResult.IsSuccess || codigoValidationResult.Value is null)
            return Result<ListarPecasInsumosResponse>.Failure(codigoValidationResult.Error);

        var pecaInsumoResult = await _pecaInsumoRepository.GetByCodigoAsync(codigoValidationResult.Value, cancellationToken);

        if (!pecaInsumoResult.IsSuccess || pecaInsumoResult.Value is null)
            return Result<ListarPecasInsumosResponse>.Failure(pecaInsumoResult.Error);

        return Result<ListarPecasInsumosResponse>.Success(CreateSingleItemResponse(pecaInsumoResult.Value));
    }

    private static Result<string> ValidateCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Result<string>.Failure(new Error("O codigo da peca ou insumo e obrigatorio."));

        var codigoNormalizado = codigo.Trim().ToUpperInvariant();

        if (codigoNormalizado.Length < 2)
            return Result<string>.Failure(new Error("O codigo da peca ou insumo deve ter pelo menos 2 caracteres."));

        if (codigoNormalizado.Length > 50)
            return Result<string>.Failure(new Error("O codigo da peca ou insumo deve ter no maximo 50 caracteres."));

        return Result<string>.Success(codigoNormalizado);
    }

    private static Result<bool> ValidatePagination(int pageNumber, int pageSize)
    {
        if (pageNumber <= 0)
            return Result<bool>.Failure(new Error("O numero da pagina deve ser maior que zero."));

        if (pageSize <= 0)
            return Result<bool>.Failure(new Error("O tamanho da pagina deve ser maior que zero."));

        return Result<bool>.Success(true);
    }

    private static ListarPecasInsumosResponse CreateSingleItemResponse(PecaInsumo pecaInsumo)
    {
        return new ListarPecasInsumosResponse
        {
            PageNumber = 1,
            PageSize = 1,
            TotalItems = 1,
            PecasInsumos = [ToItemResponse(pecaInsumo)]
        };
    }

    private static ListarPecaInsumoItemResponse ToItemResponse(PecaInsumo pecaInsumo)
    {
        return new ListarPecaInsumoItemResponse
        {
            Id = pecaInsumo.Id,
            Nome = pecaInsumo.Nome,
            Codigo = pecaInsumo.Codigo,
            Descricao = pecaInsumo.Descricao,
            PrecoUnitario = pecaInsumo.PrecoUnitario,
            QuantidadeEstoque = pecaInsumo.QuantidadeEstoque,
            Ativo = pecaInsumo.Ativo
        };
    }
}
