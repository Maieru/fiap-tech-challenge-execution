using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.RecuperarPecaInsumo;

public sealed class RecuperarPecaInsumoUseCase(IPecaInsumoRepository pecaInsumoRepository) : IRecuperarPecaInsumoUseCase
{
    private readonly IPecaInsumoRepository _pecaInsumoRepository = pecaInsumoRepository;

    public async Task<Result<RecuperarPecaInsumoResponse>> ExecuteAsync(RecuperarPecaInsumoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PecaInsumoId == Guid.Empty)
            return Result<RecuperarPecaInsumoResponse>.Failure(new Error("O identificador da peca ou insumo deve ser valido."));

        var pecaInsumoResult = await _pecaInsumoRepository.GetByIdAsync(command.PecaInsumoId, cancellationToken);

        if (!pecaInsumoResult.IsSuccess || pecaInsumoResult.Value is null)
            return Result<RecuperarPecaInsumoResponse>.Failure(pecaInsumoResult.Error);

        return Result<RecuperarPecaInsumoResponse>.Success(ToResponse(pecaInsumoResult.Value));
    }

    private static RecuperarPecaInsumoResponse ToResponse(PecaInsumo pecaInsumo)
    {
        return new RecuperarPecaInsumoResponse
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

