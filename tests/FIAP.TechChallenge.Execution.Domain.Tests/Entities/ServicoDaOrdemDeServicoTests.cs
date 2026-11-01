using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Tests.Entities;

[TestFixture]
internal sealed class ServicoDaOrdemDeServicoTests
{
    [Test]
    public void Create_ShouldFail_WhenServicoIsNull()
    {
        var result = ServicoDaOrdemDeServico.Create(Guid.NewGuid(), null!, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O serviço da ordem de serviço é obrigatório."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenOrdemServicoIdIsEmpty()
    {
        var servico = CreateServicoValido();

        var result = ServicoDaOrdemDeServico.Create(Guid.Empty, servico, 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O serviço deve estar associado a uma ordem de serviço válida."));
        });
    }

    [Test]
    public void Create_ShouldFail_WhenQuantidadeIsZero()
    {
        var servico = CreateServicoValido();

        var result = ServicoDaOrdemDeServico.Create(Guid.NewGuid(), servico, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade do serviço da ordem de serviço deve ser maior que zero."));
        });
    }

    [Test]
    public void Create_ShouldSucceed_WhenInputIsValid()
    {
        var ordemServicoId = Guid.NewGuid();
        var servico = CreateServicoValido();

        var result = ServicoDaOrdemDeServico.Create(ordemServicoId, servico, 3);

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
            Assert.That(entity.ServicoId, Is.EqualTo(servico.Id));
            Assert.That(entity.Descricao, Is.EqualTo("Troca de Óleo"));
            Assert.That(entity.ValorUnitario, Is.EqualTo(150m));
            Assert.That(entity.Quantidade, Is.EqualTo(3));
            Assert.That(entity.ValorTotal, Is.EqualTo(450m));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenIdIsEmpty()
    {
        var snapshot = CreateSnapshot() with { Id = Guid.Empty };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O id do serviço da ordem de serviço é inválido."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenServicoIdIsEmpty()
    {
        var snapshot = CreateSnapshot() with { ServicoId = Guid.Empty };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O serviço informado é inválido."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenDescricaoIsWhitespace()
    {
        var snapshot = CreateSnapshot() with { Descricao = "   " };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição do serviço da ordem de serviço é obrigatória."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenDescricaoHasMoreThanOneThousandCharacters()
    {
        var snapshot = CreateSnapshot() with { Descricao = new string('a', 1001) };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição do serviço da ordem de serviço deve conter no máximo 1000 caracteres."));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenValorUnitarioIsNegative()
    {
        var snapshot = CreateSnapshot() with { ValorUnitario = -0.01m };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O valor unitário do serviço da ordem de serviço não pode ser negativo."));
        });
    }

    [Test]
    public void Rehydrate_ShouldSucceed_WhenInputIsValid()
    {
        var snapshot = CreateSnapshot() with
        {
            Descricao = "  Balanceamento  ",
            ValorUnitario = 75.5m,
            Quantidade = 2
        };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

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
            Assert.That(entity.ServicoId, Is.EqualTo(snapshot.ServicoId));
            Assert.That(entity.Descricao, Is.EqualTo("Balanceamento"));
            Assert.That(entity.ValorUnitario, Is.EqualTo(75.5m));
            Assert.That(entity.Quantidade, Is.EqualTo(2));
            Assert.That(entity.ValorTotal, Is.EqualTo(151m));
        });
    }

    [Test]
    public void UpdateQuantidade_ShouldFail_WhenQuantidadeIsNegative()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateQuantidade(-1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade do serviço da ordem de serviço deve ser maior que zero."));
            Assert.That(entity.Quantidade, Is.EqualTo(2));
        });
    }

    [Test]
    public void UpdateQuantidade_ShouldSucceed_WhenQuantidadeIsValid()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateQuantidade(5);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Quantidade, Is.EqualTo(5));
            Assert.That(entity.ValorTotal, Is.EqualTo(500m));
        });
    }

    [Test]
    public void UpdateValorUnitario_ShouldFail_WhenValorUnitarioIsNegative()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateValorUnitario(-1m);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("O valor unitário do serviço da ordem de serviço não pode ser negativo."));
            Assert.That(entity.ValorUnitario, Is.EqualTo(100m));
        });
    }

    [Test]
    public void UpdateValorUnitario_ShouldSucceed_WhenValorUnitarioIsValid()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateValorUnitario(80m);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.ValorUnitario, Is.EqualTo(80m));
            Assert.That(entity.ValorTotal, Is.EqualTo(160m));
        });
    }

    [Test]
    public void UpdateDescricao_ShouldFail_WhenDescricaoIsWhitespace()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateDescricao("   ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição do serviço da ordem de serviço é obrigatória."));
            Assert.That(entity.Descricao, Is.EqualTo("serviço Inicial"));
        });
    }

    [Test]
    public void UpdateDescricao_ShouldFail_WhenDescricaoHasMoreThanOneThousandCharacters()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateDescricao(new string('a', 1001));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("A descrição do serviço da ordem de serviço deve conter no máximo 1000 caracteres."));
            Assert.That(entity.Descricao, Is.EqualTo("serviço Inicial"));
        });
    }

    [Test]
    public void UpdateDescricao_ShouldSucceed_WhenDescricaoIsValid()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.UpdateDescricao("  serviço Atualizado  ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Descricao, Is.EqualTo("serviço Atualizado"));
        });
    }

    [Test]
    public void Rehydrate_ShouldFail_WhenServicoConcluidoWithoutTempoGasto()
    {
        var snapshot = CreateSnapshot() with
        {
            Descricao = "Servico",
            ValorUnitario = 100m,
            Quantidade = 1,
            TempoGastoMinutos = null,
            Concluido = true
        };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O tempo gasto do serviço concluído da ordem de serviço deve ser maior que zero."));
        });
    }

    [Test]
    public void Concluir_ShouldFail_WhenTempoGastoMinutosIsInvalid()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.Concluir(0);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.False);
            Assert.That(result.Error.Description, Is.EqualTo("O tempo gasto do serviço da ordem de serviço deve ser maior que zero."));
            Assert.That(entity.Concluido, Is.False);
            Assert.That(entity.TempoGastoMinutos, Is.Null);
        });
    }

    [Test]
    public void Concluir_ShouldSucceed_WhenTempoGastoMinutosIsValid()
    {
        var entity = CreateServicoDaOrdemDeServicoValido();

        var result = entity.Concluir(35);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(entity.Concluido, Is.True);
            Assert.That(entity.TempoGastoMinutos, Is.EqualTo(35));
        });
    }

    [Test]
    public void Concluir_ShouldFail_WhenServicoJaConcluido()
    {
        var snapshot = CreateSnapshot() with
        {
            Descricao = "Servico inicial",
            ValorUnitario = 100m,
            Quantidade = 2,
            TempoGastoMinutos = 40,
            Concluido = true
        };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.That(result.IsSuccess, Is.True);

        var entity = result.Value!;
        var concluirResult = entity.Concluir(10);

        Assert.Multiple(() =>
        {
            Assert.That(concluirResult.IsSuccess, Is.False);
            Assert.That(concluirResult.Value, Is.False);
            Assert.That(concluirResult.Error.Description, Is.EqualTo("O serviço da ordem de serviço já foi concluído."));
            Assert.That(entity.TempoGastoMinutos, Is.EqualTo(40));
        });
    }

    private static Servico CreateServicoValido()
    {
        var result = Servico.Create("  Troca de Óleo  ", 150m);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        });

        return result.Value!;
    }

    private static ServicoDaOrdemDeServico CreateServicoDaOrdemDeServicoValido()
    {
        var snapshot = CreateSnapshot() with
        {
            Descricao = "serviço Inicial",
            ValorUnitario = 100m,
            Quantidade = 2
        };

        var result = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
        });

        return result.Value!;
    }

    private static ServicoDaOrdemDeServicoSnapshot CreateSnapshot()
    {
        return new ServicoDaOrdemDeServicoSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "serviço",
            10m,
            1,
            null,
            false);
    }
}

