using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.AtualizarPecaInsumo;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.PecasInsumos.AtualizarPecaInsumo;

[TestFixture]
internal sealed class AtualizarPecaInsumoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPecaInsumoIsNotFound()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Failure(new Error("Peca ou insumo nao encontrado.")));

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);
        var command = CreateCommand();

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Peca ou insumo nao encontrado."));
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenCodigoAlreadyExists()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(codigo: "FLT-999");

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id, codigo: "FLT-001")));
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Ja existe uma peca ou insumo cadastrado com este codigo."));
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync("FLT-999", It.IsAny<CancellationToken>()), Times.Once);
        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenNomeIsInvalid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(nome: " ");

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id)));

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description.Contains("nome", StringComparison.OrdinalIgnoreCase), Is.True);
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenCodigoIsInvalid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(codigo: " ");

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id)));

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.Not.EqualTo(Error.None));
        });

        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenDescricaoIsInvalid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(descricao: new string('a', 501));

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id)));

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.Not.EqualTo(Error.None));
        });

        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPrecoUnitarioIsInvalid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(precoUnitario: -1m);

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id)));

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description.Contains("negativo", StringComparison.OrdinalIgnoreCase), Is.True);
        });

        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldNotCheckCodigoDuplicity_WhenCodigoWasNotChanged()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        PecaInsumo? updatedPecaInsumo = null;
        var command = CreateCommand(codigo: " flt-001 ");

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id, codigo: "FLT-001")));
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()))
            .Callback<PecaInsumo, CancellationToken>((pecaInsumo, _) => updatedPecaInsumo = pecaInsumo)
            .Returns(Task.CompletedTask);

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(updatedPecaInsumo, Is.Not.Null);
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        PecaInsumo? updatedPecaInsumo = null;
        var command = CreateCommand(
            nome: "Filtro de combustivel premium",
            codigo: "fcp-010",
            descricao: "Elemento filtrante atualizado",
            precoUnitario: 89.90m,
            ativo: false);

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(CreatePecaInsumo(command.Id, nome: "Filtro de combustivel", codigo: "FLT-001", quantidadeEstoque: 12)));
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()))
            .Callback<PecaInsumo, CancellationToken>((pecaInsumo, _) => updatedPecaInsumo = pecaInsumo)
            .Returns(Task.CompletedTask);

        var useCase = new AtualizarPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(updatedPecaInsumo, Is.Not.Null);
        });

        pecaInsumoRepositoryMock.Verify(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync("FCP-010", It.IsAny<CancellationToken>()), Times.Once);
        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Once);

        var response = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(command.Id));
            Assert.That(response.Nome, Is.EqualTo("Filtro de combustivel premium"));
            Assert.That(response.Codigo, Is.EqualTo("FCP-010"));
            Assert.That(response.Descricao, Is.EqualTo("Elemento filtrante atualizado"));
            Assert.That(response.PrecoUnitario, Is.EqualTo(89.90m));
            Assert.That(response.QuantidadeEstoque, Is.EqualTo(12));
            Assert.That(response.Ativo, Is.False);
        });
    }

    private static AtualizarPecaInsumoCommand CreateCommand(
        Guid? id = null,
        string nome = "Filtro de combustivel",
        string codigo = "FLT-001",
        string? descricao = "Filtro para sistema de injecao",
        decimal precoUnitario = 59.9m,
        bool ativo = true) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Nome = nome,
            Codigo = codigo,
            Descricao = descricao,
            PrecoUnitario = precoUnitario,
            Ativo = ativo
        };

    private static PecaInsumo CreatePecaInsumo(
        Guid id,
        string nome = "Filtro de combustivel",
        string codigo = "FLT-001",
        string? descricao = "Filtro para sistema de injecao",
        decimal precoUnitario = 59.9m,
        int quantidadeEstoque = 10,
        bool ativo = true)
    {
        var pecaInsumoResult = PecaInsumo.Rehydrate(id, nome, codigo, descricao, precoUnitario, quantidadeEstoque, ativo);

        Assert.Multiple(() =>
        {
            Assert.That(pecaInsumoResult.IsSuccess, Is.True);
            Assert.That(pecaInsumoResult.Value, Is.Not.Null);
        });

        return pecaInsumoResult.Value!;
    }
}
