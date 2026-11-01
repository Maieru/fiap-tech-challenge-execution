using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Domain.Entities;

/// <summary>
/// Representa um serviço do catálogo administrativo.
/// Não pertence diretamente a uma ordem de serviço.
/// </summary>
public sealed class Servico
{
    public Guid Id { get; private set; }
    public string Descricao { get; private set; }
    public decimal ValorUnitario { get; private set; }

    private Servico(Guid id, string descricao, decimal valorUnitario)
    {
        Id = id;
        Descricao = descricao;
        ValorUnitario = valorUnitario;
    }

    public static Result<Servico> Create(string descricao, decimal valorUnitario)
    {
        return Create(Guid.NewGuid(), descricao, valorUnitario);
    }

    public static Result<Servico> Rehydrate(Guid id, string descricao, decimal valorUnitario)
    {
        if (id == Guid.Empty)
            return Result<Servico>.Failure(new Error("O id do serviço é inválido."));

        return Create(id, descricao, valorUnitario);
    }

    private static Result<Servico> Create(Guid id, string descricao, decimal valorUnitario)
    {
        var descricaoResult = ValidateDescricao(descricao);

        if (!descricaoResult.IsSuccess || descricaoResult.Value is null)
            return Result<Servico>.Failure(descricaoResult.Error);

        var valorUnitarioResult = ValidateValorUnitario(valorUnitario);

        if (!valorUnitarioResult.IsSuccess)
            return Result<Servico>.Failure(valorUnitarioResult.Error);

        var item = new Servico(id, descricaoResult.Value, valorUnitario);
        return Result<Servico>.Success(item);
    }

    public Result<bool> UpdateDescricao(string descricao)
    {
        var descricaoResult = ValidateDescricao(descricao);

        if (!descricaoResult.IsSuccess || descricaoResult.Value is null)
            return Result<bool>.Failure(descricaoResult.Error);

        Descricao = descricaoResult.Value;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateValorUnitario(decimal valorUnitario)
    {
        var valorUnitarioResult = ValidateValorUnitario(valorUnitario);

        if (!valorUnitarioResult.IsSuccess)
            return Result<bool>.Failure(valorUnitarioResult.Error);

        ValorUnitario = valorUnitario;
        return Result<bool>.Success(true);
    }

    private static Result<string> ValidateDescricao(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            return Result<string>.Failure(new Error("A descrição do serviço é obrigatória."));

        descricao = descricao.Trim();

        if (descricao.Length > 1000)
            return Result<string>.Failure(new Error("A descrição do serviço deve conter no máximo 1000 caracteres."));

        return Result<string>.Success(descricao);
    }

    private static Result<bool> ValidateValorUnitario(decimal valorUnitario)
    {
        if (valorUnitario < 0)
            return Result<bool>.Failure(new Error("O valor do serviço não pode ser negativo."));

        return Result<bool>.Success(true);
    }
}

