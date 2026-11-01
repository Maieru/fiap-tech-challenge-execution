using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.RecuperarPecaInsumo;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.PecasInsumos.RecuperarPecaInsumo;

[TestFixture]
internal sealed class RecuperarPecaInsumoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPecaInsumoIdIsEmpty()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var useCase = new RecuperarPecaInsumoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new RecuperarPecaInsumoCommand { PecaInsumoId = Guid.Empty });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O identificador da peca ou insumo deve ser valido."));
        });

        repositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPecaInsumoDoesNotExist()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Failure(new Error("Peca ou insumo nao encontrado.")));

        var useCase = new RecuperarPecaInsumoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new RecuperarPecaInsumoCommand { PecaInsumoId = Guid.NewGuid() });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Peca ou insumo nao encontrado."));
        });
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenPecaInsumoExists()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var pecaInsumo = CreatePecaInsumo();

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(pecaInsumo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(pecaInsumo));

        var useCase = new RecuperarPecaInsumoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new RecuperarPecaInsumoCommand { PecaInsumoId = pecaInsumo.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Id, Is.EqualTo(pecaInsumo.Id));
            Assert.That(result.Value.Nome, Is.EqualTo("Filtro de oleo"));
            Assert.That(result.Value.Codigo, Is.EqualTo("FLT-001"));
            Assert.That(result.Value.Descricao, Is.EqualTo("Filtro para troca preventiva"));
            Assert.That(result.Value.PrecoUnitario, Is.EqualTo(49.9m));
            Assert.That(result.Value.QuantidadeEstoque, Is.EqualTo(8));
            Assert.That(result.Value.Ativo, Is.True);
        });
    }

    private static PecaInsumo CreatePecaInsumo()
    {
        var pecaInsumoResult = PecaInsumo.Create("Filtro de oleo", "FLT-001", "Filtro para troca preventiva", 49.9m, 8);

        Assert.Multiple(() =>
        {
            Assert.That(pecaInsumoResult.IsSuccess, Is.True);
            Assert.That(pecaInsumoResult.Value, Is.Not.Null);
        });

        return pecaInsumoResult.Value!;
    }
}

