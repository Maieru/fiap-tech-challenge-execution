using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.ExcluirServico;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Servicos.ExcluirServico;

[TestFixture]
internal sealed class ExcluirServicoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldCallDelete_WhenServicoExists()
    {
        var repositoryMock = new Mock<IServicoRepository>();
        var servico = Servico.Create("Troca de oleo", 80m).Value!;

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(servico));

        var useCase = new ExcluirServicoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ExcluirServicoCommand { Id = servico.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Id, Is.EqualTo(servico.Id));
        });
        repositoryMock.Verify(x => x.DeleteAsync(servico, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenServicoDoesNotExist()
    {
        var repositoryMock = new Mock<IServicoRepository>();

        _ = repositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Failure(new Error("Servico nao encontrado.", ErrorCode.NotFound)));

        var useCase = new ExcluirServicoUseCase(repositoryMock.Object);

        var result = await useCase.ExecuteAsync(new ExcluirServicoCommand { Id = Guid.NewGuid() });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.NotFound));
        });
        repositoryMock.Verify(x => x.DeleteAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

