using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ExcluirServico;

public sealed class ExcluirServicoUseCase(IServicoRepository servicoRepository) : IExcluirServicoUseCase
{
    private readonly IServicoRepository _servicoRepository = servicoRepository;

    public async Task<Result<ExcluirServicoResponse>> ExecuteAsync(ExcluirServicoCommand command, CancellationToken cancellationToken = default)
    {
        var servicoResult = await _servicoRepository.GetByIdAsync(command.Id, cancellationToken);

        if (!servicoResult.IsSuccess || servicoResult.Value is null)
            return Result<ExcluirServicoResponse>.Failure(servicoResult.Error);

        await _servicoRepository.DeleteAsync(servicoResult.Value, cancellationToken);

        return Result<ExcluirServicoResponse>.Success(new ExcluirServicoResponse { Id = command.Id });
    }
}

