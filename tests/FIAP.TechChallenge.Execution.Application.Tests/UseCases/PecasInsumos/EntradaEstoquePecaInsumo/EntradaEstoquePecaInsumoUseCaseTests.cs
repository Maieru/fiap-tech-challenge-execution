using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.PecasInsumos.EntradaEstoquePecaInsumo;

[TestFixture]
internal sealed class EntradaEstoquePecaInsumoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPecaInsumoIsNotFound()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand();

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Failure(new Error("Peca ou insumo nao encontrado.")));

        var useCase = new EntradaEstoquePecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Peca ou insumo nao encontrado."));
        });

        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenQuantidadeIsInvalid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(quantidade: 0);
        var pecaInsumo = CreatePecaInsumo(id: command.Id, quantidadeEstoque: 5);

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(pecaInsumo));

        var useCase = new EntradaEstoquePecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("A quantidade de entrada em estoque deve ser maior que zero."));
        });

        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var command = CreateCommand(quantidade: 7);
        var pecaInsumo = CreatePecaInsumo(id: command.Id, quantidadeEstoque: 5);
        PecaInsumo? updatedPecaInsumo = null;

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(pecaInsumo));
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()))
            .Callback<PecaInsumo, CancellationToken>((entity, _) => updatedPecaInsumo = entity)
            .Returns(Task.CompletedTask);

        var useCase = new EntradaEstoquePecaInsumoUseCase(pecaInsumoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(updatedPecaInsumo, Is.Not.Null);
        });

        pecaInsumoRepositoryMock.Verify(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
        pecaInsumoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Once);

        var response = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(command.Id));
            Assert.That(response.Nome, Is.EqualTo("Filtro de oleo"));
            Assert.That(response.Codigo, Is.EqualTo("FLT-001"));
            Assert.That(response.QuantidadeEntrada, Is.EqualTo(7));
            Assert.That(response.QuantidadeEstoque, Is.EqualTo(12));
            Assert.That(updatedPecaInsumo!.QuantidadeEstoque, Is.EqualTo(12));
        });
    }

    private static EntradaEstoquePecaInsumoCommand CreateCommand(Guid? id = null, int quantidade = 5) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Quantidade = quantidade
        };

    private static PecaInsumo CreatePecaInsumo(Guid? id = null, int quantidadeEstoque = 10)
    {
        var entityResult = PecaInsumo.Rehydrate(
            id ?? Guid.NewGuid(),
            "Filtro de oleo",
            "FLT-001",
            "Filtro para troca preventiva",
            45.9m,
            quantidadeEstoque,
            true);

        Assert.Multiple(() =>
        {
            Assert.That(entityResult.IsSuccess, Is.True);
            Assert.That(entityResult.Value, Is.Not.Null);
        });

        return entityResult.Value!;
    }
}

