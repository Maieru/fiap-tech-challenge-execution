using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.VerificarTempoMedioServico;

public sealed class VerificarTempoMedioServicoUseCase(
    IServicoRepository servicoRepository,
    IServicoDaOrdemDeServicoRepository servicoDaOrdemDeServicoRepository) : IVerificarTempoMedioServicoUseCase
{
    private readonly IServicoRepository _servicoRepository = servicoRepository;
    private readonly IServicoDaOrdemDeServicoRepository _servicoDaOrdemDeServicoRepository = servicoDaOrdemDeServicoRepository;

    public async Task<Result<VerificarTempoMedioServicoResponse>> ExecuteAsync(VerificarTempoMedioServicoCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ServicoId == Guid.Empty)
            return Result<VerificarTempoMedioServicoResponse>.Failure(new Error("O identificador do servico deve ser valido."));

        var servicoResult = await _servicoRepository.GetByIdAsync(command.ServicoId, cancellationToken);

        if (!servicoResult.IsSuccess || servicoResult.Value is null)
            return Result<VerificarTempoMedioServicoResponse>.Failure(servicoResult.Error);

        var servicosConcluidosResult = await _servicoDaOrdemDeServicoRepository.GetConcluidosByServicoIdAsync(command.ServicoId, cancellationToken);

        if (!servicosConcluidosResult.IsSuccess || servicosConcluidosResult.Value is null)
            return Result<VerificarTempoMedioServicoResponse>.Failure(servicosConcluidosResult.Error);

        var quantidadeExecucoes = servicosConcluidosResult.Value.Count;

        if (quantidadeExecucoes == 0)
        {
            return Result<VerificarTempoMedioServicoResponse>.Success(new VerificarTempoMedioServicoResponse
            {
                ServicoId = command.ServicoId,
                TempoMedioMinutos = 0m,
                QuantidadeExecucoes = 0
            });
        }

        var tempoTotal = servicosConcluidosResult.Value.Sum(x => (decimal)x.TempoGastoMinutos!.Value);
        var tempoMedioMinutos = Math.Round(tempoTotal / quantidadeExecucoes, 2, MidpointRounding.AwayFromZero);

        return Result<VerificarTempoMedioServicoResponse>.Success(new VerificarTempoMedioServicoResponse
        {
            ServicoId = command.ServicoId,
            TempoMedioMinutos = tempoMedioMinutos,
            QuantidadeExecucoes = quantidadeExecucoes
        });
    }
}

