using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.RecuperarServico;

public sealed class RecuperarServicoUseCase(IServicoRepository servicoRepository) : IRecuperarServicoUseCase
{
    private readonly IServicoRepository _servicoRepository = servicoRepository;

    public async Task<Result<RecuperarServicoResponse>> ExecuteAsync(RecuperarServicoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ServicoId == Guid.Empty)
            return Result<RecuperarServicoResponse>.Failure(new Error("O identificador do servico deve ser valido."));

        var servicoResult = await _servicoRepository.GetByIdAsync(command.ServicoId, cancellationToken);

        if (!servicoResult.IsSuccess || servicoResult.Value is null)
            return Result<RecuperarServicoResponse>.Failure(servicoResult.Error);

        return Result<RecuperarServicoResponse>.Success(ToResponse(servicoResult.Value));
    }

    private static RecuperarServicoResponse ToResponse(Servico servico)
    {
        return new RecuperarServicoResponse
        {
            Id = servico.Id,
            Descricao = servico.Descricao,
            ValorUnitario = servico.ValorUnitario
        };
    }
}
