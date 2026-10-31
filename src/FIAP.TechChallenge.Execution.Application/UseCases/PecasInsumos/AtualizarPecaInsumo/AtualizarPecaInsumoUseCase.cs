using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.AtualizarPecaInsumo;

public sealed class AtualizarPecaInsumoUseCase(IPecaInsumoRepository pecaInsumoRepository) : IAtualizarPecaInsumoUseCase
{
    private readonly IPecaInsumoRepository _pecaInsumoRepository = pecaInsumoRepository;

    public async Task<Result<AtualizarPecaInsumoResponse>> ExecuteAsync(AtualizarPecaInsumoCommand command, CancellationToken cancellationToken = default)
    {
        var pecaInsumoResult = await _pecaInsumoRepository.GetByIdAsync(command.Id, cancellationToken);

        if (!pecaInsumoResult.IsSuccess || pecaInsumoResult.Value is null)
            return Result<AtualizarPecaInsumoResponse>.Failure(pecaInsumoResult.Error);

        var pecaInsumo = pecaInsumoResult.Value;
        var codigoNormalizado = NormalizeCodigo(command.Codigo);
        var codigoFoiAlterado = !string.Equals(pecaInsumo.Codigo, codigoNormalizado, StringComparison.OrdinalIgnoreCase);

        if (codigoFoiAlterado && !string.IsNullOrWhiteSpace(codigoNormalizado))
        {
            var codigoJaExiste = await _pecaInsumoRepository.ExistsByCodigoAsync(codigoNormalizado, cancellationToken);

            if (codigoJaExiste)
                return Result<AtualizarPecaInsumoResponse>.Failure(new Error("Ja existe uma peca ou insumo cadastrado com este codigo.", ErrorCode.Conflict));
        }

        var updateNomeResult = pecaInsumo.UpdateNome(command.Nome);

        if (!updateNomeResult.IsSuccess)
            return Result<AtualizarPecaInsumoResponse>.Failure(updateNomeResult.Error);

        var updateCodigoResult = pecaInsumo.UpdateCodigo(command.Codigo);

        if (!updateCodigoResult.IsSuccess)
            return Result<AtualizarPecaInsumoResponse>.Failure(updateCodigoResult.Error);

        var updateDescricaoResult = pecaInsumo.UpdateDescricao(command.Descricao);

        if (!updateDescricaoResult.IsSuccess)
            return Result<AtualizarPecaInsumoResponse>.Failure(updateDescricaoResult.Error);

        var updatePrecoResult = pecaInsumo.UpdatePrecoUnitario(command.PrecoUnitario);

        if (!updatePrecoResult.IsSuccess)
            return Result<AtualizarPecaInsumoResponse>.Failure(updatePrecoResult.Error);

        var updateAtivoResult = command.Ativo ? pecaInsumo.Activate() : pecaInsumo.Inactivate();

        if (!updateAtivoResult.IsSuccess)
            return Result<AtualizarPecaInsumoResponse>.Failure(updateAtivoResult.Error);

        await _pecaInsumoRepository.UpdateAsync(pecaInsumo, cancellationToken);

        return Result<AtualizarPecaInsumoResponse>.Success(new AtualizarPecaInsumoResponse
        {
            Id = pecaInsumo.Id,
            Nome = pecaInsumo.Nome,
            Codigo = pecaInsumo.Codigo,
            Descricao = pecaInsumo.Descricao,
            PrecoUnitario = pecaInsumo.PrecoUnitario,
            QuantidadeEstoque = pecaInsumo.QuantidadeEstoque,
            Ativo = pecaInsumo.Ativo
        });
    }

    private static string NormalizeCodigo(string codigo)
    {
        return codigo?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
