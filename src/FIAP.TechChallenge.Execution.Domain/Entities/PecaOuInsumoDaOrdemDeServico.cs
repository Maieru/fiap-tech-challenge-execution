using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Domain.Entities;

public sealed record PecaOuInsumoDaOrdemDeServicoSnapshot(
    Guid Id,
    Guid OrdemServicoId,
    Guid PecaInsumoId,
    string Nome,
    string Codigo,
    string? Descricao,
    decimal PrecoUnitario,
    int Quantidade);

/// <summary>
/// Representa uma peça ou insumo utilizado em uma ordem de serviço,
/// preservando os dados usados no momento de adição.
/// </summary>
public sealed class PecaOuInsumoDaOrdemDeServico
{
    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public Guid PecaInsumoId { get; private set; }

    public string Nome { get; private set; }
    public string Codigo { get; private set; }
    public string? Descricao { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public int Quantidade { get; private set; }

    public decimal ValorTotal => PrecoUnitario * Quantidade;

    private PecaOuInsumoDaOrdemDeServico(PecaOuInsumoDaOrdemDeServicoSnapshot snapshot)
    {
        Id = snapshot.Id;
        OrdemServicoId = snapshot.OrdemServicoId;
        PecaInsumoId = snapshot.PecaInsumoId;
        Nome = snapshot.Nome;
        Codigo = snapshot.Codigo;
        Descricao = snapshot.Descricao;
        PrecoUnitario = snapshot.PrecoUnitario;
        Quantidade = snapshot.Quantidade;
    }

    public static Result<PecaOuInsumoDaOrdemDeServico> Create(Guid ordemServicoId, PecaInsumo pecaInsumo, int quantidade)
    {
        if (pecaInsumo is null)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("A peça ou insumo da ordem de serviço é obrigatória."));

        var snapshot = new PecaOuInsumoDaOrdemDeServicoSnapshot(
            Guid.NewGuid(),
            ordemServicoId,
            pecaInsumo.Id,
            pecaInsumo.Nome,
            pecaInsumo.Codigo,
            pecaInsumo.Descricao,
            pecaInsumo.PrecoUnitario,
            quantidade);

        return Create(snapshot);
    }

    public static Result<PecaOuInsumoDaOrdemDeServico> Rehydrate(PecaOuInsumoDaOrdemDeServicoSnapshot snapshot)
    {
        if (snapshot.Id == Guid.Empty)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O id da peça ou insumo da ordem de serviço é inválido."));

        return Create(snapshot);
    }

    private static Result<PecaOuInsumoDaOrdemDeServico> Create(PecaOuInsumoDaOrdemDeServicoSnapshot snapshot)
    {
        if (snapshot.OrdemServicoId == Guid.Empty)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("A peça ou insumo deve estar associada a uma ordem de serviço válida."));

        if (snapshot.PecaInsumoId == Guid.Empty)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("A peça ou insumo informado é inválido."));

        if (string.IsNullOrWhiteSpace(snapshot.Nome))
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O nome da peça ou insumo da ordem de serviço é obrigatório."));

        var nome = snapshot.Nome.Trim();

        if (nome.Length < 3)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O nome da peça ou insumo da ordem de serviço deve ter pelo menos 3 caracteres."));

        if (nome.Length > 150)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O nome da peça ou insumo da ordem de serviço deve ter no máximo 150 caracteres."));

        if (string.IsNullOrWhiteSpace(snapshot.Codigo))
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O código da peça ou insumo da ordem de serviço é obrigatório."));

        var codigo = snapshot.Codigo.Trim().ToUpperInvariant();

        if (codigo.Length < 2)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O código da peça ou insumo da ordem de serviço deve ter pelo menos 2 caracteres."));

        if (codigo.Length > 50)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O código da peça ou insumo da ordem de serviço deve ter no máximo 50 caracteres."));

        var descricao = snapshot.Descricao;

        if (descricao is not null)
        {
            descricao = descricao.Trim();

            if (descricao.Length > 500)
                return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("A descrição da peça ou insumo da ordem de serviço deve ter no máximo 500 caracteres."));

            if (string.IsNullOrWhiteSpace(descricao))
                descricao = null;
        }

        if (snapshot.PrecoUnitario < 0)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("O preço unitário da peça ou insumo da ordem de serviço não pode ser negativo."));

        if (snapshot.Quantidade <= 0)
            return Result<PecaOuInsumoDaOrdemDeServico>.Failure(new Error("A quantidade da peça ou insumo da ordem de serviço deve ser maior que zero."));

        var normalizedSnapshot = snapshot with
        {
            Nome = nome,
            Codigo = codigo,
            Descricao = descricao
        };

        var entity = new PecaOuInsumoDaOrdemDeServico(normalizedSnapshot);
        return Result<PecaOuInsumoDaOrdemDeServico>.Success(entity);
    }

    public Result<bool> UpdateQuantidade(int quantidade)
    {
        if (quantidade <= 0)
            return Result<bool>.Failure(new Error("A quantidade da peça ou insumo da ordem de serviço deve ser maior que zero."));

        Quantidade = quantidade;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdatePrecoUnitario(decimal precoUnitario)
    {
        if (precoUnitario < 0)
            return Result<bool>.Failure(new Error("O preço unitário da peça ou insumo da ordem de serviço não pode ser negativo."));

        PrecoUnitario = precoUnitario;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateDescricao(string? descricao)
    {
        if (descricao is null)
        {
            Descricao = null;
            return Result<bool>.Success(true);
        }

        descricao = descricao.Trim();

        if (string.IsNullOrWhiteSpace(descricao))
        {
            Descricao = null;
            return Result<bool>.Success(true);
        }

        if (descricao.Length > 500)
            return Result<bool>.Failure(new Error("A descrição da peça ou insumo da ordem de serviço deve ter no máximo 500 caracteres."));

        Descricao = descricao;
        return Result<bool>.Success(true);
    }
}
