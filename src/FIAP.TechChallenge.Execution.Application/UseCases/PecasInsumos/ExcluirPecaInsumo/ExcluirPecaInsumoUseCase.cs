using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ExcluirPecaInsumo;

public sealed class ExcluirPecaInsumoUseCase(IPecaInsumoRepository pecaInsumoRepository) : IExcluirPecaInsumoUseCase
{
    private readonly IPecaInsumoRepository _pecaInsumoRepository = pecaInsumoRepository;

    public async Task<Result<ExcluirPecaInsumoResponse>> ExecuteAsync(ExcluirPecaInsumoCommand command, CancellationToken cancellationToken = default)
    {
        var pecaInsumoResult = await _pecaInsumoRepository.GetByIdAsync(command.Id, cancellationToken);

        if (!pecaInsumoResult.IsSuccess || pecaInsumoResult.Value is null)
            return Result<ExcluirPecaInsumoResponse>.Failure(pecaInsumoResult.Error);

        await _pecaInsumoRepository.DeleteAsync(pecaInsumoResult.Value, cancellationToken);

        return Result<ExcluirPecaInsumoResponse>.Success(new ExcluirPecaInsumoResponse { Id = command.Id });
    }
}

