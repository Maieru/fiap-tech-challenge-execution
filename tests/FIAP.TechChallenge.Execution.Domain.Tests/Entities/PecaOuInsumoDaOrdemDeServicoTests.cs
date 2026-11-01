using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Tests.Entities;

[TestFixture]
internal sealed class PecaOuInsumoDaOrdemDeServicoTests
{
    [Test]
    public void Create_ShouldFail_WhenPecaInsumoIsNull()
    {
        var result = PecaOuInsumoDaOrdemDeServico.Create(Guid.NewGuid(), null!, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A peça ou insumo da ordem de serviço é obrigatória."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenOrdemServicoIdIsEmpty()
    {
        var pecaInsumo = CreatePecaInsumoValida();

        var result = PecaOuInsumoDaOrdemDeServico.Create(Guid.Empty, pecaInsumo, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A peça ou insumo deve estar associada a uma ordem de serviço válida."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenQuantidadeIsZero()
    {
        var pecaInsumo = CreatePecaInsumoValida();

        var result = PecaOuInsumoDaOrdemDeServico.Create(Guid.NewGuid(), pecaInsumo, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade da peça ou insumo da ordem de serviço deve ser maior que zero."));
        });
    }

    [Test]
    public void Create_ShouldSucceed_WhenInputIsValid()
    {
        var ordemServicoId = Guid.NewGuid();
        var pecaInsumo = CreatePecaInsumoValida();

        var result = PecaOuInsumoDaOrdemDeServico.Create(ordemServicoId, pecaInsumo, 4);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Error, Is.EqualTo(Error.None));
        });

        var entity = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(entity.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(entity.OrdemServicoId, Is.EqualTo(ordemServicoId));
            Assert.That(entity.PecaInsumoId, Is.EqualTo(pecaInsumo.Id));
            Assert.That(entity.Nome, Is.EqualTo("Filtro de Óleo"));
            Assert.That(entity.Codigo, Is.EqualTo("FLT01"));
            Assert.That(entity.Descricao, Is.EqualTo("Marca X"));
            Assert.That(entity.PrecoUnitario, Is.EqualTo(25m));
            Assert.That(entity.Quantidade, Is.EqualTo(4));
            Assert.That(entity.ValorTotal, Is.EqualTo(100m));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenIdIsEmpty()
    {
        var snapshot = CreateSnapshot() with { Id = Guid.Empty };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O id da peça ou insumo da ordem de serviço é inválido."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenOrdemServicoIdIsEmpty()
    {
        var snapshot = CreateSnapshot() with { OrdemServicoId = Guid.Empty };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A peça ou insumo deve estar associada a uma ordem de serviço válida."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenPecaInsumoIdIsEmpty()
    {
        var snapshot = CreateSnapshot() with { PecaInsumoId = Guid.Empty };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A peça ou insumo informado é inválido."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenNomeIsWhitespace()
    {
        var snapshot = CreateSnapshot() with { Nome = "   " };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo da ordem de serviço é obrigatório."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenNomeHasLessThanThreeCharacters()
    {
        var snapshot = CreateSnapshot() with { Nome = "Ab" };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo da ordem de serviço deve ter pelo menos 3 caracteres."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenNomeHasMoreThanOneHundredAndFiftyCharacters()
    {
        var snapshot = CreateSnapshot() with { Nome = new string('a', 151) };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O nome da peça ou insumo da ordem de serviço deve ter no máximo 150 caracteres."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenCodigoIsWhitespace()
    {
        var snapshot = CreateSnapshot() with { Codigo = "   " };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O código da peça ou insumo da ordem de serviço é obrigatório."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenCodigoHasLessThanTwoCharacters()
    {
        var snapshot = CreateSnapshot() with { Codigo = "A" };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O código da peça ou insumo da ordem de serviço deve ter pelo menos 2 caracteres."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenCodigoHasMoreThanFiftyCharacters()
    {
        var snapshot = CreateSnapshot() with { Codigo = new string('A', 51) };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O código da peça ou insumo da ordem de serviço deve ter no máximo 50 caracteres."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenDescricaoHasMoreThanFiveHundredCharacters()
    {
        var snapshot = CreateSnapshot() with { Descricao = new string('a', 501) };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição da peça ou insumo da ordem de serviço deve ter no máximo 500 caracteres."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenPrecoUnitarioIsNegative()
    {
        var snapshot = CreateSnapshot() with { PrecoUnitario = -0.01m };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O preço unitário da peça ou insumo da ordem de serviço não pode ser negativo."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenQuantidadeIsNegative()
    {
        var snapshot = CreateSnapshot() with { Quantidade = -1 };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade da peça ou insumo da ordem de serviço deve ser maior que zero."));
        });
    }

    [Test]
    public void Rehydrate_ShouldSucceed_WhenSnapshotIsValidAndNormalizeValues()
    {
        var snapshot = CreateSnapshot() with
        {
            Nome = "  Filtro premium  ",
            Codigo = "  flt99  ",
            Descricao = "  Descrição técnica  "
        };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Error, Is.EqualTo(Error.None));
        });

        var entity = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(entity.Id, Is.EqualTo(snapshot.Id));
            Assert.That(entity.OrdemServicoId, Is.EqualTo(snapshot.OrdemServicoId));
            Assert.That(entity.PecaInsumoId, Is.EqualTo(snapshot.PecaInsumoId));
            Assert.That(entity.Nome, Is.EqualTo("Filtro premium"));
            Assert.That(entity.Codigo, Is.EqualTo("FLT99"));
            Assert.That(entity.Descricao, Is.EqualTo("Descrição técnica"));
            Assert.That(entity.PrecoUnitario, Is.EqualTo(snapshot.PrecoUnitario));
            Assert.That(entity.Quantidade, Is.EqualTo(snapshot.Quantidade));
            Assert.That(entity.ValorTotal, Is.EqualTo(snapshot.PrecoUnitario * snapshot.Quantidade));
        });
    }

    [Test]
    public void Rehydrate_ShouldSucceed_WhenDescricaoIsWhitespaceAndSetDescricaoToNull()
    {
        var snapshot = CreateSnapshot() with { Descricao = "   " };

        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Descricao, Is.Null);
        });
    }

    [Test]
    public void UpdateQuantidade_ShouldFail_WhenQuantidadeIsZero()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdateQuantidade(0);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade da peça ou insumo da ordem de serviço deve ser maior que zero."));
            Assert.That(entity.Quantidade, Is.EqualTo(2));
        });
    }

    [Test]
    public void UpdateQuantidade_ShouldSucceed_WhenQuantidadeIsValid()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdateQuantidade(6);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Quantidade, Is.EqualTo(6));
            Assert.That(entity.ValorTotal, Is.EqualTo(120m));
        });
    }

    [Test]
    public void UpdatePrecoUnitario_ShouldFail_WhenPrecoUnitarioIsNegative()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdatePrecoUnitario(-1m);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("O preço unitário da peça ou insumo da ordem de serviço não pode ser negativo."));
            Assert.That(entity.PrecoUnitario, Is.EqualTo(20m));
        });
    }

    [Test]
    public void UpdatePrecoUnitario_ShouldSucceed_WhenPrecoUnitarioIsValid()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdatePrecoUnitario(30m);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.PrecoUnitario, Is.EqualTo(30m));
            Assert.That(entity.ValorTotal, Is.EqualTo(60m));
        });
    }

    [Test]
    public void UpdateDescricao_ShouldSucceed_WhenDescricaoIsNull()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdateDescricao(null);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Descricao, Is.Null);
        });
    }

    [Test]
    public void UpdateDescricao_ShouldSucceed_WhenDescricaoIsWhitespace()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdateDescricao("   ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Descricao, Is.Null);
        });
    }

    [Test]
    public void UpdateDescricao_ShouldFail_WhenDescricaoHasMoreThanFiveHundredCharacters()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdateDescricao(new string('a', 501));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição da peça ou insumo da ordem de serviço deve ter no máximo 500 caracteres."));
            Assert.That(entity.Descricao, Is.EqualTo("Descrição inicial"));
        });
    }

    [Test]
    public void UpdateDescricao_ShouldSucceed_WhenDescricaoIsValid()
    {
        var entity = CreatePecaOuInsumoDaOrdemDeServicoValida();

        var result = entity.UpdateDescricao("  Descrição atualizada  ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Descricao, Is.EqualTo("Descrição atualizada"));
        });
    }

    private static PecaInsumo CreatePecaInsumoValida()
    {
        var result = PecaInsumo.Create("  Filtro de Óleo  ", "  flt01  ", "  Marca X  ", 25m, 10);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        });

        return result.Value!;
    }

    private static PecaOuInsumoDaOrdemDeServicoSnapshot CreateSnapshot()
    {
        return new PecaOuInsumoDaOrdemDeServicoSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Filtro",
            "FLT01",
            "Descrição",
            20m,
            2);
    }

    private static PecaOuInsumoDaOrdemDeServico CreatePecaOuInsumoDaOrdemDeServicoValida()
    {
        var result = PecaOuInsumoDaOrdemDeServico.Rehydrate(
            new PecaOuInsumoDaOrdemDeServicoSnapshot(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Insumo inicial",
                "INS01",
                "Descrição inicial",
                20m,
                2));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        });

        return result.Value!;
    }
}

