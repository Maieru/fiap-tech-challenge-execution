using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.CadastrarServico;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Servicos.CadastrarServico;

[TestFixture]
internal sealed class CadastrarServicoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenCommandIsInvalid()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var useCase = new CadastrarServicoUseCase(servicoRepositoryMock.Object);
        var command = CreateCommand(descricao: " ");

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error, Is.Not.EqualTo(Error.None));
        });

        servicoRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenCommandIsValid()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        Servico? addedServico = null;

        _ = servicoRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()))
            .Callback<Servico, CancellationToken>((servico, _) => addedServico = servico)
            .Returns(Task.CompletedTask);

        var useCase = new CadastrarServicoUseCase(servicoRepositoryMock.Object);
        var command = CreateCommand(descricao: "Troca de correia dentada", valorUnitario: 320m);

        var result = await useCase.ExecuteAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Error, Is.EqualTo(Error.None));
            Assert.That(addedServico, Is.Not.Null);
        });

        servicoRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Servico>(), It.IsAny<CancellationToken>()), Times.Once);

        var response = result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(addedServico!.Id));
            Assert.That(response.Descricao, Is.EqualTo("Troca de correia dentada"));
            Assert.That(response.ValorUnitario, Is.EqualTo(320m));
        });
    }

    private static CadastrarServicoCommand CreateCommand(
        string descricao = "Alinhamento e balanceamento",
        decimal valorUnitario = 150m) =>
        new()
        {
            Descricao = descricao,
            ValorUnitario = valorUnitario,
        };
}

