using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;

public sealed class EntradaEstoquePecaInsumoUseCase(IPecaInsumoRepository pecaInsumoRepository) : IEntradaEstoquePecaInsumoUseCase
{
    private readonly IPecaInsumoRepository _pecaInsumoRepository = pecaInsumoRepository;

    public async Task<Result<EntradaEstoquePecaInsumoResponse>> ExecuteAsync(EntradaEstoquePecaInsumoCommand command, CancellationToken cancellationToken = default)
    {
        var pecaInsumoResult = await _pecaInsumoRepository.GetByIdAsync(command.Id, cancellationToken);

        if (!pecaInsumoResult.IsSuccess || pecaInsumoResult.Value is null)
            return Result<EntradaEstoquePecaInsumoResponse>.Failure(pecaInsumoResult.Error);

        var pecaInsumo = pecaInsumoResult.Value;
        var addEstoqueResult = pecaInsumo.AddEstoque(command.Quantidade);

        if (!addEstoqueResult.IsSuccess)
            return Result<EntradaEstoquePecaInsumoResponse>.Failure(addEstoqueResult.Error);

        await _pecaInsumoRepository.UpdateAsync(pecaInsumo, cancellationToken);

        return Result<EntradaEstoquePecaInsumoResponse>.Success(new EntradaEstoquePecaInsumoResponse
        {
            Id = pecaInsumo.Id,
            Nome = pecaInsumo.Nome,
            Codigo = pecaInsumo.Codigo,
            QuantidadeEntrada = command.Quantidade,
            QuantidadeEstoque = pecaInsumo.QuantidadeEstoque
        });
    }
}

