using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ListarServicos;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Servicos.ListarServicos;

[TestFixture]
internal sealed class ListarServicosUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenPaginationIsInvalid()
    {
        var repositoryMock = new Mock<IServicoRepository>();
        var useCase = new ListarServicosUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ListarServicosCommand { PageNumber = 0, PageSize = 10 });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O numero da pagina deve ser maior que zero."));
        });

        repositoryMock.Verify(x => x.GetPagedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenListingPagedServices()
    {
        var repositoryMock = new Mock<IServicoRepository>();
        var servico1 = CreateServico(descricao: "Alinhamento", valorUnitario: 80m);
        var servico2 = CreateServico(descricao: "Balanceamento", valorUnitario: 60m);

        _ = repositoryMock
            .Setup(x => x.GetPagedAsync(1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(IReadOnlyCollection<Servico> Servicos, int TotalItems)>.Success((new[] { servico1, servico2 }, 2)));

        var useCase = new ListarServicosUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ListarServicosCommand { PageNumber = 1, PageSize = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.TotalItems, Is.EqualTo(2));
            Assert.That(result.Value.PageNumber, Is.EqualTo(1));
            Assert.That(result.Value.PageSize, Is.EqualTo(2));
            Assert.That(result.Value.Servicos, Has.Count.EqualTo(2));
            Assert.That(result.Value.Servicos.Select(x => x.Descricao), Is.EqualTo(new[] { "Alinhamento", "Balanceamento" }));
        });

        repositoryMock.Verify(x => x.GetPagedAsync(1, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Servico CreateServico(Guid? id = null, string descricao = "Servico", decimal valorUnitario = 10m)
    {
        var servicoResult = Servico.Rehydrate(id ?? Guid.NewGuid(), descricao, valorUnitario);

        Assert.Multiple(() =>
        {
            Assert.That(servicoResult.IsSuccess, Is.True);
            Assert.That(servicoResult.Value, Is.Not.Null);
        });

        return servicoResult.Value!;
    }
}

