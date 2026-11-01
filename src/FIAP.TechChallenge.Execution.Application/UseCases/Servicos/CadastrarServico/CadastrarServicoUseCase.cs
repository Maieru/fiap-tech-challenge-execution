using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;

namespace FIAP.TechChallenge.Execution.Application.UseCases.Servicos.CadastrarServico;

public sealed class CadastrarServicoUseCase(IServicoRepository servicoRepository) : ICadastrarServicoUseCase
{
    private readonly IServicoRepository _servicoRepository = servicoRepository;

    public async Task<Result<CadastrarServicoResponse>> ExecuteAsync(CadastrarServicoCommand command, CancellationToken cancellationToken = default)
    {
        var servicoResult = Servico.Create(command.Descricao, command.ValorUnitario);

        if (!servicoResult.IsSuccess || servicoResult.Value is null)
            return Result<CadastrarServicoResponse>.Failure(servicoResult.Error);

        var servico = servicoResult.Value;

        await _servicoRepository.AddAsync(servico, cancellationToken);

        return Result<CadastrarServicoResponse>.Success(new CadastrarServicoResponse
        {
            Id = servico.Id,
            Descricao = servico.Descricao,
            ValorUnitario = servico.ValorUnitario
        });
    }
}

