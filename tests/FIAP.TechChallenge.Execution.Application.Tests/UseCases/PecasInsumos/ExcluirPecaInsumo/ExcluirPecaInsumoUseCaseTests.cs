using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ExcluirPecaInsumo;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.PecasInsumos.ExcluirPecaInsumo;

[TestFixture]
internal sealed class ExcluirPecaInsumoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldCallDelete_WhenPecaInsumoExists()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var pecaInsumo = PecaInsumo.Create("Filtro de oleo", "FLT-01", "Filtro", 25m, 10).Value!;

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(pecaInsumo.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(pecaInsumo));

        var useCase = new ExcluirPecaInsumoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ExcluirPecaInsumoCommand { Id = pecaInsumo.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Id, Is.EqualTo(pecaInsumo.Id));
        });
        repositoryMock.Verify(x => x.DeleteAsync(pecaInsumo, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenPecaInsumoDoesNotExist()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Failure(new Error("Peca ou insumo nao encontrado.", ErrorCode.NotFound)));

        var useCase = new ExcluirPecaInsumoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ExcluirPecaInsumoCommand { Id = Guid.NewGuid() });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.NotFound));
        });
        repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

