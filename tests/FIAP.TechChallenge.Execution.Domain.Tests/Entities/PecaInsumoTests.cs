using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Tests.Entities;

[TestFixture]
internal class PecaInsumoTests
{
    [Test]
    public void Create_ShouldFail_WhenNomeIsWhitespace()
    {
        var result = PecaInsumo.Create("   ", "ABC123", "Descrição", 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo é obrigatório."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenNomeIsInvalid()
    {
        var result = PecaInsumo.Create("Ab", "ABC123", "Descrição", 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo deve ter pelo menos 3 caracteres."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenCodigoIsWhitespace()
    {
        var result = PecaInsumo.Create("Parafuso", "   ", "Descrição", 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O código da peça ou insumo é obrigatório."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenCodigoIsInvalid()
    {
        var result = PecaInsumo.Create("Parafuso", "A", "Descrição", 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O código da peça ou insumo deve ter pelo menos 2 caracteres."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenDescricaoIsNotNullAndInvalid()
    {
        var result = PecaInsumo.Create("Parafuso", "ABC123", new string('a', 501), 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição da peça ou insumo deve ter no máximo 500 caracteres."));
        });
    }

    [Test]
    public void Create_ShouldSucceed_WhenDescricaoIsNotNullAndWhitespace()
    {
        var result = PecaInsumo.Create("Parafuso", "ABC123", "   ", 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(FIAP.TechChallenge.Execution.Domain.Abstractions.Error.None));
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Descricao, Is.Null);
        });
    }

    [Test]
    public void Create_ShouldFail_WhenPrecoUnitarioIsNegative()
    {
        var result = PecaInsumo.Create("Parafuso", "ABC123", "Descrição", -1m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O preço unitário da peça ou insumo não pode ser negativo."));
        });
    }

    [Test]
    public void UpdateNome_ShouldFail_WhenNomeIsInvalidAndNotWhitespace()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.UpdateNome("Ab");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo deve ter pelo menos 3 caracteres."));
            Assert.That(entity.Nome, Is.EqualTo("Peça Inicial"));
        });
    }

    [Test]
    public void UpdateNome_ShouldSucceed_WhenNomeIsValid()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.UpdateNome("  Nome Atualizado  ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(FIAP.TechChallenge.Execution.Domain.Abstractions.Error.None));
            Assert.That(entity.Nome, Is.EqualTo("Nome Atualizado"));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenQuantidadeEstoqueIsNegative()
    {
        var result = PecaInsumo.Create("Parafuso", "ABC123", "Descrição", 10m, -1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade em estoque não pode ser negativa."));
        });
    }

    [Test]
    public void Create_ShouldSucceed_WhenInputIsValid()
    {
        var result = PecaInsumo.Create("  Parafuso Sextavado  ", "  abc123  ", "  Descrição válida  ", 12.5m, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(FIAP.TechChallenge.Execution.Domain.Abstractions.Error.None));
            Assert.That(result.Value, Is.Not.Null);
        });

        var entity = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(entity.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(entity.Nome, Is.EqualTo("Parafuso Sextavado"));
            Assert.That(entity.Codigo, Is.EqualTo("ABC123"));
            Assert.That(entity.Descricao, Is.EqualTo("Descrição válida"));
            Assert.That(entity.PrecoUnitario, Is.EqualTo(12.5m));
            Assert.That(entity.QuantidadeEstoque, Is.EqualTo(10));
            Assert.That(entity.Ativo, Is.True);
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenIdIsEmpty()
    {
        var result = PecaInsumo.Rehydrate(Guid.Empty, "Parafuso", "ABC123", "Descrição", 10m, 5, true);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O id da peça ou insumo é inválido."));
        });
    }

    [Test]
    public void Rehydrate_ShouldSucceed_WhenInputIsValidAndInativo()
    {
        var id = Guid.NewGuid();

        var result = PecaInsumo.Rehydrate(id, "  Arruela  ", "  ar01  ", "  item  ", 2.5m, 30, false);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(FIAP.TechChallenge.Execution.Domain.Abstractions.Error.None));
            Assert.That(result.Value, Is.Not.Null);
        });

        var entity = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(entity.Id, Is.EqualTo(id));
            Assert.That(entity.Nome, Is.EqualTo("Arruela"));
            Assert.That(entity.Codigo, Is.EqualTo("AR01"));
            Assert.That(entity.Descricao, Is.EqualTo("item"));
            Assert.That(entity.Ativo, Is.False);
        });
    }

    [Test]
    public void UpdateNome_ShouldFail_WhenNomeIsWhitespace()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.UpdateNome("   ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo é obrigatório."));
            Assert.That(entity.Nome, Is.EqualTo("Peça Inicial"));
        });
    }

    [Test]
    public void UpdateCodigo_ShouldSucceed_WhenCodigoIsValid()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.UpdateCodigo("  xyz99  ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(FIAP.TechChallenge.Execution.Domain.Abstractions.Error.None));
            Assert.That(entity.Codigo, Is.EqualTo("XYZ99"));
        });
    }

    [Test]
    public void UpdateDescricao_ShouldSetNull_WhenDescricaoIsWhitespace()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.UpdateDescricao("   ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(FIAP.TechChallenge.Execution.Domain.Abstractions.Error.None));
            Assert.That(entity.Descricao, Is.Null);
        });
    }

    [Test]
    public void AddEstoque_ShouldFail_WhenQuantidadeIsZero()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.AddEstoque(0);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade de entrada em estoque deve ser maior que zero."));
            Assert.That(entity.QuantidadeEstoque, Is.EqualTo(5));
        });
    }

    [Test]
    public void RemoveEstoque_ShouldFail_WhenQuantidadeIsGreaterThanEstoque()
    {
        var entity = CreatePecaInsumoValido();

        var result = entity.RemoveEstoque(6);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade informada é maior que o estoque disponível."));
            Assert.That(entity.QuantidadeEstoque, Is.EqualTo(5));
        });
    }

    [Test]
    public void Inactivate_And_Activate_ShouldToggleAtivoStatus()
    {
        var entity = CreatePecaInsumoValido();

        var inactivateResult = entity.Inactivate();
        var activateResult = entity.Activate();

        Assert.Multiple(() =>
        {
            Assert.That(inactivateResult.IsSuccess, Is.True);
            Assert.That(activateResult.IsSuccess, Is.True);
            Assert.That(entity.Ativo, Is.True);
        });
    }

    private static PecaInsumo CreatePecaInsumoValido()
    {
        var result = PecaInsumo.Create("Peça Inicial", "COD01", "Descrição", 10m, 5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        });

        return result.Value!;
    }
}
