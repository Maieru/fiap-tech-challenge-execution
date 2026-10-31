using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.AtualizarServico;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Servicos.AtualizarServico;

[TestFixture]
internal sealed class AtualizarServicoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenServicoIsNotFound()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Failure(new Error("Servico nao encontrado.")));

        var useCase = new AtualizarServicoUseCase(servicoRepositoryMock.Object);
        var command = CreateCommand();

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Servico nao encontrado."));
        });

        servicoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenDescricaoIsInvalid()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var command = CreateCommand(descricao: " ");

        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(CreateServico(command.Id)));

        var useCase = new AtualizarServicoUseCase(servicoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.Not.EqualTo(Error.None));
        });

        servicoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenValorUnitarioIsInvalid()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var command = CreateCommand(valorUnitario: -1m);

        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(CreateServico(command.Id)));

        var useCase = new AtualizarServicoUseCase(servicoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description.Contains("negativo", StringComparison.OrdinalIgnoreCase), Is.True);
        });

        servicoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        Servico? updatedServico = null;
        var command = CreateCommand(descricao: "Troca de velas", valorUnitario: 220m);

        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(CreateServico(command.Id, descricao: "Servico antigo", valorUnitario: 100m)));
        _ = servicoRepositoryMock
            .Setup(x => x.UpdateAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()))
            .Callback<Servico, CancellationToken>((servico, _) => updatedServico = servico)
            .Returns(Task.CompletedTask);

        var useCase = new AtualizarServicoUseCase(servicoRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(updatedServico, Is.Not.Null);
        });

        servicoRepositoryMock.Verify(x => x.GetByIdAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
        servicoRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);

        var response = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(command.Id));
            Assert.That(response.Descricao, Is.EqualTo("Troca de velas"));
            Assert.That(response.ValorUnitario, Is.EqualTo(220m));
        });
    }

    private static AtualizarServicoCommand CreateCommand(
        Guid? id = null,
        string descricao = "Alinhamento",
        decimal valorUnitario = 150m) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Descricao = descricao,
            ValorUnitario = valorUnitario,
        };

    private static Servico CreateServico(Guid id, string descricao = "Alinhamento", decimal valorUnitario = 150m)
    {
        var servicoResult = Servico.Rehydrate(id, descricao, valorUnitario);

        Assert.Multiple(() =>
        {
            Assert.That(servicoResult.IsSuccess, Is.True);
            Assert.That(servicoResult.Value, Is.Not.Null);
        });

        return servicoResult.Value!;
    }
}
