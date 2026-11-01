using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.AtualizarServico;

public sealed class AtualizarServicoUseCase(IServicoRepository servicoRepository) : IAtualizarServicoUseCase
{
    private readonly IServicoRepository _servicoRepository = servicoRepository;

    public async Task<Result<AtualizarServicoResponse>> ExecuteAsync(AtualizarServicoCommand command, CancellationToken cancellationToken = default)
    {
        var servicoResult = await _servicoRepository.GetByIdAsync(command.Id, cancellationToken);

        if (!servicoResult.IsSuccess || servicoResult.Value is null)
            return Result<AtualizarServicoResponse>.Failure(servicoResult.Error);

        var servico = servicoResult.Value;

        var updateDescricaoResult = servico.UpdateDescricao(command.Descricao);

        if (!updateDescricaoResult.IsSuccess)
            return Result<AtualizarServicoResponse>.Failure(updateDescricaoResult.Error);

        var updateValorUnitarioResult = servico.UpdateValorUnitario(command.ValorUnitario);

        if (!updateValorUnitarioResult.IsSuccess)
            return Result<AtualizarServicoResponse>.Failure(updateValorUnitarioResult.Error);

        await _servicoRepository.UpdateAsync(servico, cancellationToken);

        return Result<AtualizarServicoResponse>.Success(new AtualizarServicoResponse
        {
            Id = servico.Id,
            Descricao = servico.Descricao,
            ValorUnitario = servico.ValorUnitario
        });
    }
}

