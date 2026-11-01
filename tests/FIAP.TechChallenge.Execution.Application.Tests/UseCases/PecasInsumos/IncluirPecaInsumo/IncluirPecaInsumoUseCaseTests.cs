using FIAP.TechChallenge.Execution.Application.UseCases.PecasInsumos.IncluirPecaInsumo;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.PecasInsumos.IncluirPecaInsumo;

[TestFixture]
internal sealed class IncluirPecaInsumoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenCommandIsInvalid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        var useCase = new IncluirPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);
        var command = CreateCommand(nome: " ");

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.Not.EqualTo(Error.None));
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        pecaInsumoRepositoryMock.Verify(x => x.AddAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenCodigoAlreadyExists()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = new IncluirPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);
        var command = CreateCommand(codigo: "pi-123");

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Ja existe uma peca ou insumo cadastrado com este codigo."));
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync("PI-123", It.IsAny<CancellationToken>()), Times.Once);
        pecaInsumoRepositoryMock.Verify(x => x.AddAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var pecaInsumoRepositoryMock = new Mock<IPecaInsumoRepository>();
        PecaInsumo? addedPecaInsumo = null;

        _ = pecaInsumoRepositoryMock
            .Setup(x => x.ExistsByCodigoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _ = pecaInsumoRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()))
            .Callback<PecaInsumo, CancellationToken>((pecaInsumo, _) => addedPecaInsumo = pecaInsumo)
            .Returns(Task.CompletedTask);

        var useCase = new IncluirPecaInsumoUseCase(pecaInsumoRepositoryMock.Object);
        var command = CreateCommand(codigo: "flt-001", descricao: "Filtro para troca de oleo");

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(addedPecaInsumo, Is.Not.Null);
        });

        pecaInsumoRepositoryMock.Verify(x => x.ExistsByCodigoAsync("FLT-001", It.IsAny<CancellationToken>()), Times.Once);
        pecaInsumoRepositoryMock.Verify(x => x.AddAsync(It.IsAny<PecaInsumo>(), It.IsAny<CancellationToken>()), Times.Once);

        var response = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(addedPecaInsumo!.Id));
            Assert.That(response.Nome, Is.EqualTo("Filtro de oleo"));
            Assert.That(response.Codigo, Is.EqualTo("FLT-001"));
            Assert.That(response.Descricao, Is.EqualTo("Filtro para troca de oleo"));
            Assert.That(response.PrecoUnitario, Is.EqualTo(45.9m));
            Assert.That(response.QuantidadeEstoque, Is.EqualTo(12));
            Assert.That(response.Ativo, Is.True);
        });
    }

    private static IncluirPecaInsumoCommand CreateCommand(
        string nome = "Filtro de oleo",
        string codigo = "FLT-001",
        string? descricao = null,
        decimal precoUnitario = 45.9m,
        int quantidadeEstoque = 12) =>
        new()
        {
            Nome = nome,
            Codigo = codigo,
            Descricao = descricao,
            PrecoUnitario = precoUnitario,
            QuantidadeEstoque = quantidadeEstoque
        };
}

