using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.RecuperarServico;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Servicos.RecuperarServico;

[TestFixture]
internal sealed class RecuperarServicoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenServicoIdIsEmpty()
    {
        var repositoryMock = new Mock<IServicoRepository>();
        var useCase = new RecuperarServicoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new RecuperarServicoCommand { ServicoId = Guid.Empty });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O identificador do servico deve ser valido."));
        });

        repositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenServicoDoesNotExist()
    {
        var repositoryMock = new Mock<IServicoRepository>();

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Failure(new Error("Servico nao encontrado.")));

        var useCase = new RecuperarServicoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new RecuperarServicoCommand { ServicoId = Guid.NewGuid() });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Servico nao encontrado."));
        });
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenServicoExists()
    {
        var repositoryMock = new Mock<IServicoRepository>();
        var servico = CreateServico(descricao: "Troca de oleo", valorUnitario: 120m);

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(servico));

        var useCase = new RecuperarServicoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new RecuperarServicoCommand { ServicoId = servico.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.Id, Is.EqualTo(servico.Id));
            Assert.That(result.Value.Descricao, Is.EqualTo("Troca de oleo"));
            Assert.That(result.Value.ValorUnitario, Is.EqualTo(120m));
        });
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
