using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Domain.Entities;

public sealed record ServicoDaOrdemDeServicoSnapshot(
    Guid Id,
    Guid OrdemServicoId,
    Guid ServicoId,
    string Descricao,
    decimal ValorUnitario,
    int Quantidade,
    int? TempoGastoMinutos,
    bool Concluido);

/// <summary>
/// Representa um serviço efetivamente adicionado a uma ordem de serviço,
/// preservando os dados usados no momento de adição.
/// </summary>
public sealed class ServicoDaOrdemDeServico
{
    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public Guid ServicoId { get; private set; }

    public string Descricao { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public int Quantidade { get; private set; }
    public int? TempoGastoMinutos { get; private set; }
    public bool Concluido { get; private set; }

    public decimal ValorTotal => ValorUnitario * Quantidade;

    private ServicoDaOrdemDeServico(ServicoDaOrdemDeServicoSnapshot snapshot)
    {
        Id = snapshot.Id;
        OrdemServicoId = snapshot.OrdemServicoId;
        ServicoId = snapshot.ServicoId;
        Descricao = snapshot.Descricao;
        ValorUnitario = snapshot.ValorUnitario;
        Quantidade = snapshot.Quantidade;
        TempoGastoMinutos = snapshot.TempoGastoMinutos;
        Concluido = snapshot.Concluido;
    }

    public static Result<ServicoDaOrdemDeServico> Create(Guid ordemServicoId, Servico servico, int quantidade)
    {
        if (servico is null)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("O serviço da ordem de serviço é obrigatório."));

        var snapshot = new ServicoDaOrdemDeServicoSnapshot(
            Guid.NewGuid(),
            ordemServicoId,
            servico.Id,
            servico.Descricao,
            servico.ValorUnitario,
            quantidade,
            null,
            false);

        return Create(snapshot);
    }

    public static Result<ServicoDaOrdemDeServico> Rehydrate(ServicoDaOrdemDeServicoSnapshot snapshot)
    {
        if (snapshot.Id == Guid.Empty)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("O id do serviço da ordem de serviço é inválido."));

        return Create(snapshot);
    }

    private static Result<ServicoDaOrdemDeServico> Create(ServicoDaOrdemDeServicoSnapshot snapshot)
    {
        if (snapshot.OrdemServicoId == Guid.Empty)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("O serviço deve estar associado a uma ordem de serviço válida."));

        if (snapshot.ServicoId == Guid.Empty)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("O serviço informado é inválido."));

        if (string.IsNullOrWhiteSpace(snapshot.Descricao))
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("A descrição do serviço da ordem de serviço é obrigatória."));

        var descricao = snapshot.Descricao.Trim();

        if (descricao.Length > 1000)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("A descrição do serviço da ordem de serviço deve conter no máximo 1000 caracteres."));

        if (snapshot.ValorUnitario < 0)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("O valor unitário do serviço da ordem de serviço não pode ser negativo."));

        if (snapshot.Quantidade <= 0)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("A quantidade do serviço da ordem de serviço deve ser maior que zero."));

        if (snapshot.Concluido && (!snapshot.TempoGastoMinutos.HasValue || snapshot.TempoGastoMinutos.Value <= 0))
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("O tempo gasto do serviço concluído da ordem de serviço deve ser maior que zero."));

        if (!snapshot.Concluido && snapshot.TempoGastoMinutos.HasValue)
            return Result<ServicoDaOrdemDeServico>.Failure(new Error("Não é permitido informar tempo gasto para serviço não concluído da ordem de serviço."));

        var normalizedSnapshot = snapshot with { Descricao = descricao };

        var entity = new ServicoDaOrdemDeServico(normalizedSnapshot);

        return Result<ServicoDaOrdemDeServico>.Success(entity);
    }

    public Result<bool> Concluir(int tempoGastoMinutos)
    {
        if (Concluido)
            return Result<bool>.Failure(new Error("O serviço da ordem de serviço já foi concluído."));

        if (tempoGastoMinutos <= 0)
            return Result<bool>.Failure(new Error("O tempo gasto do serviço da ordem de serviço deve ser maior que zero."));

        TempoGastoMinutos = tempoGastoMinutos;
        Concluido = true;

        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateQuantidade(int quantidade)
    {
        if (quantidade <= 0)
            return Result<bool>.Failure(new Error("A quantidade do serviço da ordem de serviço deve ser maior que zero."));

        Quantidade = quantidade;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateValorUnitario(decimal valorUnitario)
    {
        if (valorUnitario < 0)
            return Result<bool>.Failure(new Error("O valor unitário do serviço da ordem de serviço não pode ser negativo."));

        ValorUnitario = valorUnitario;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            return Result<bool>.Failure(new Error("A descrição do serviço da ordem de serviço é obrigatória."));

        descricao = descricao.Trim();

        if (descricao.Length > 1000)
            return Result<bool>.Failure(new Error("A descrição do serviço da ordem de serviço deve conter no máximo 1000 caracteres."));

        Descricao = descricao;
        return Result<bool>.Success(true);
    }
}

