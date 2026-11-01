using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.ListarPecasInsumos;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.PecasInsumos.ListarPecasInsumos;

[TestFixture]
internal sealed class ListarPecasInsumosUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPaginationIsInvalid()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var useCase = new ListarPecasInsumosUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ListarPecasInsumosCommand { PageNumber = 0, PageSize = 10 });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O numero da pagina deve ser maior que zero."));
        });

        repositoryMock.Verify(x => x.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenListingPagedPecasInsumos()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var pecaInsumo1 = CreatePecaInsumo(codigo: "FLT-001");
        var pecaInsumo2 = CreatePecaInsumo(codigo: "PST-002", nome: "Pastilha de freio");

        _ = repositoryMock
            .Setup(x => x.GetPagedAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(IReadOnlyCollection<PecaInsumo> PecasInsumos, int TotalItems)>.Success((new[] { pecaInsumo1, pecaInsumo2 }, 2)));

        var useCase = new ListarPecasInsumosUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ListarPecasInsumosCommand { PageNumber = 1, PageSize = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.PageNumber, Is.EqualTo(1));
            Assert.That(result.Value.PageSize, Is.EqualTo(2));
            Assert.That(result.Value.TotalItems, Is.EqualTo(2));
            Assert.That(result.Value.PecasInsumos, Has.Count.EqualTo(2));
        });

        repositoryMock.Verify(x => x.GetPagedAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenGettingByCodigo()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var pecaInsumo = CreatePecaInsumo(codigo: "FLT-001");

        _ = repositoryMock
            .Setup(x => x.GetByCodigoAsync("FLT-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PecaInsumo>.Success(pecaInsumo));

        var useCase = new ListarPecasInsumosUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ListarPecasInsumosCommand { Codigo = "flt-001" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.PecasInsumos, Has.Count.EqualTo(1));
            Assert.That(result.Value.PecasInsumos.First().Codigo, Is.EqualTo("FLT-001"));
        });

        repositoryMock.Verify(x => x.GetByCodigoAsync("FLT-001", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenCodigoIsInvalid()
    {
        var repositoryMock = new Mock<IPecaInsumoRepository>();
        var useCase = new ListarPecasInsumosUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ListarPecasInsumosCommand { Codigo = "x" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O codigo da peca ou insumo deve ter pelo menos 2 caracteres."));
        });

        repositoryMock.Verify(x => x.GetByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static PecaInsumo CreatePecaInsumo(
        string nome = "Filtro de oleo",
        string codigo = "FLT-001",
        string? descricao = "Filtro para troca preventiva",
        decimal precoUnitario = 49.9m,
        int quantidadeEstoque = 8)
    {
        var pecaInsumoResult = PecaInsumo.Create(nome, codigo, descricao, precoUnitario, quantidadeEstoque);

        Assert.Multiple(() =>
        {
            Assert.That(pecaInsumoResult.IsSuccess, Is.True);
            Assert.That(pecaInsumoResult.Value, Is.Not.Null);
        });

        return pecaInsumoResult.Value!;
    }
}

