using FIAP.TechChallenge.Execution.Domain.Abstractions;

namespace FIAP.TechChallenge.Execution.Domain.Entities;

/// <summary>
/// Representa uma peça ou insumo do catálogo administrativo.
/// Não pertence diretamente a uma ordem de serviço.
/// </summary>
public sealed class PecaInsumo
{
    public Guid Id { get; private set; }
    public string Nome { get; private set; }
    public string Codigo { get; private set; }
    public string? Descricao { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public int QuantidadeEstoque { get; private set; }
    public bool Ativo { get; private set; }

    private PecaInsumo(Guid id, string nome, string codigo, string? descricao, decimal precoUnitario, int quantidadeEstoque)
    {
        Id = id;
        Nome = nome;
        Codigo = codigo;
        Descricao = descricao;
        PrecoUnitario = precoUnitario;
        QuantidadeEstoque = quantidadeEstoque;
        Ativo = true;
    }

    public static Result<PecaInsumo> Create(string nome, string codigo, string? descricao, decimal precoUnitario, int quantidadeEstoque)
    {
        return Create(Guid.NewGuid(), nome, codigo, descricao, precoUnitario, quantidadeEstoque);
    }

    public static Result<PecaInsumo> Rehydrate(Guid id, string nome, string codigo, string? descricao, decimal precoUnitario, int quantidadeEstoque, bool ativo)
    {
        if (id == Guid.Empty)
            return Result<PecaInsumo>.Failure(new Error("O id da peça ou insumo é inválido."));

        var result = Create(id, nome, codigo, descricao, precoUnitario, quantidadeEstoque);

        if (!result.IsSuccess)
            return result;

        var entity = result.Value!;

        if (!ativo)
            _ = entity.Inactivate();

        return Result<PecaInsumo>.Success(entity);
    }

    private static Result<PecaInsumo> Create(Guid id, string nome, string codigo, string? descricao, decimal precoUnitario, int quantidadeEstoque)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<PecaInsumo>.Failure(new Error("O nome da peça ou insumo é obrigatório."));

        nome = nome.Trim();

        var nomeValidationResult = IsNomeValid(nome);

        if (!nomeValidationResult.IsSuccess)
            return Result<PecaInsumo>.Failure(nomeValidationResult.Error);

        if (string.IsNullOrWhiteSpace(codigo))
            return Result<PecaInsumo>.Failure(new Error("O código da peça ou insumo é obrigatório."));

        codigo = codigo.Trim().ToUpperInvariant();

        var codigoValidationResult = IsCodigoValid(codigo);

        if (!codigoValidationResult.IsSuccess)
            return Result<PecaInsumo>.Failure(codigoValidationResult.Error);

        if (descricao is not null)
        {
            descricao = descricao.Trim();

            var descricaoValidationResult = IsDescricaoValid(descricao);
            if (!descricaoValidationResult.IsSuccess)
                return Result<PecaInsumo>.Failure(descricaoValidationResult.Error);

            if (string.IsNullOrWhiteSpace(descricao))
                descricao = null;
        }

        if (precoUnitario < 0)
            return Result<PecaInsumo>.Failure(new Error("O preço unitário da peça ou insumo não pode ser negativo."));

        if (quantidadeEstoque < 0)
            return Result<PecaInsumo>.Failure(new Error("A quantidade em estoque não pode ser negativa."));

        var entity = new PecaInsumo(id, nome, codigo, descricao, precoUnitario, quantidadeEstoque);
        return Result<PecaInsumo>.Success(entity);
    }

    public Result<bool> UpdateNome(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return Result<bool>.Failure(new Error("O nome da peça ou insumo é obrigatório."));

        nome = nome.Trim();

        var nomeValidationResult = IsNomeValid(nome);

        if (!nomeValidationResult.IsSuccess)
            return Result<bool>.Failure(nomeValidationResult.Error);

        Nome = nome;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Result<bool>.Failure(new Error("O código da peça ou insumo é obrigatório."));

        codigo = codigo.Trim().ToUpperInvariant();

        var codigoValidationResult = IsCodigoValid(codigo);

        if (!codigoValidationResult.IsSuccess)
            return Result<bool>.Failure(codigoValidationResult.Error);

        Codigo = codigo;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdateDescricao(string? descricao)
    {
        if (descricao is null)
        {
            Descricao = descricao;
            return Result<bool>.Success(true);
        }

        descricao = descricao.Trim();

        if (!string.IsNullOrWhiteSpace(descricao))
        {
            var descricaoValidationResult = IsDescricaoValid(descricao);
            if (!descricaoValidationResult.IsSuccess)
                return Result<bool>.Failure(descricaoValidationResult.Error);
        }
        else
        {
            descricao = null;
        }

        Descricao = descricao;
        return Result<bool>.Success(true);
    }

    public Result<bool> UpdatePrecoUnitario(decimal precoUnitario)
    {
        if (precoUnitario < 0)
            return Result<bool>.Failure(new Error("O preço unitário da peça ou insumo não pode ser negativo."));

        PrecoUnitario = precoUnitario;
        return Result<bool>.Success(true);
    }

    public Result<bool> AddEstoque(int quantidade)
    {
        if (quantidade <= 0)
            return Result<bool>.Failure(new Error("A quantidade de entrada em estoque deve ser maior que zero."));

        QuantidadeEstoque += quantidade;
        return Result<bool>.Success(true);
    }

    public Result<bool> RemoveEstoque(int quantidade)
    {
        if (quantidade <= 0)
            return Result<bool>.Failure(new Error("A quantidade de saída em estoque deve ser maior que zero."));

        if (quantidade > QuantidadeEstoque)
            return Result<bool>.Failure(new Error("A quantidade informada é maior que o estoque disponível."));

        QuantidadeEstoque -= quantidade;
        return Result<bool>.Success(true);
    }

    public Result<bool> Activate()
    {
        Ativo = true;
        return Result<bool>.Success(true);
    }

    public Result<bool> Inactivate()
    {
        Ativo = false;
        return Result<bool>.Success(true);
    }

    private static Result<bool> IsNomeValid(string nome)
    {
        if (nome.Length < 3)
            return Result<bool>.Failure(new Error("O nome da peça ou insumo deve ter pelo menos 3 caracteres."));

        if (nome.Length > 150)
            return Result<bool>.Failure(new Error("O nome da peça ou insumo deve ter no máximo 150 caracteres."));

        return Result<bool>.Success(true);
    }

    private static Result<bool> IsCodigoValid(string codigo)
    {
        if (codigo.Length < 2)
            return Result<bool>.Failure(new Error("O código da peça ou insumo deve ter pelo menos 2 caracteres."));

        if (codigo.Length > 50)
            return Result<bool>.Failure(new Error("O código da peça ou insumo deve ter no máximo 50 caracteres."));

        return Result<bool>.Success(true);
    }

    private static Result<bool> IsDescricaoValid(string descricao)
    {
        if (descricao.Length > 500)
            return Result<bool>.Failure(new Error("A descrição da peça ou insumo deve ter no máximo 500 caracteres."));

        return Result<bool>.Success(true);
    }
}
