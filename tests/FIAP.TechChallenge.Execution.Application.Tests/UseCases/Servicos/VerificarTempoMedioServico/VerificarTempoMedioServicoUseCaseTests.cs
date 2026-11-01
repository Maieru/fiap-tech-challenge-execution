using FIAP.TechChallenge.Execution.Application.UseCases.Servicos.VerificarTempoMedioServico;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Servicos.VerificarTempoMedioServico;

[TestFixture]
internal sealed class VerificarTempoMedioServicoUseCaseTests
{
    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenServicoIdIsEmpty()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var servicoDaOrdemRepositoryMock = new Mock<IServicoDaOrdemDeServicoRepository>();
        var useCase = new VerificarTempoMedioServicoUseCase(servicoRepositoryMock.Object, servicoDaOrdemRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(new VerificarTempoMedioServicoCommand { ServicoId = Guid.Empty });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("O identificador do servico deve ser valido."));
        });

        servicoRepositoryMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        servicoDaOrdemRepositoryMock.Verify(x => x.GetConcluidosByServicoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldFail_WhenServicoDoesNotExist()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var servicoDaOrdemRepositoryMock = new Mock<IServicoDaOrdemDeServicoRepository>();

        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Failure(new Error("Servico nao encontrado.", ErrorCode.NotFound)));

        var useCase = new VerificarTempoMedioServicoUseCase(servicoRepositoryMock.Object, servicoDaOrdemRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(new VerificarTempoMedioServicoCommand { ServicoId = Guid.NewGuid() });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Value, Is.Null);
            Assert.That(result.Error.Description, Is.EqualTo("Servico nao encontrado."));
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCode.NotFound));
        });

        servicoDaOrdemRepositoryMock.Verify(x => x.GetConcluidosByServicoIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenServicoHasNoCompletedExecutions()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var servicoDaOrdemRepositoryMock = new Mock<IServicoDaOrdemDeServicoRepository>();
        var servico = CreateServico(descricao: "Troca de oleo", valorUnitario: 120m);

        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(servico));

        _ = servicoDaOrdemRepositoryMock
            .Setup(x => x.GetConcluidosByServicoIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>.Success(Array.Empty<ServicoDaOrdemDeServico>()));

        var useCase = new VerificarTempoMedioServicoUseCase(servicoRepositoryMock.Object, servicoDaOrdemRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(new VerificarTempoMedioServicoCommand { ServicoId = servico.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.ServicoId, Is.EqualTo(servico.Id));
            Assert.That(result.Value.QuantidadeExecucoes, Is.EqualTo(0));
            Assert.That(result.Value.TempoMedioMinutos, Is.EqualTo(0m));
        });
    }

    [Test]
    public async Task ExecuteAsync_ShouldSucceed_WhenServicoHasCompletedExecutions()
    {
        var servicoRepositoryMock = new Mock<IServicoRepository>();
        var servicoDaOrdemRepositoryMock = new Mock<IServicoDaOrdemDeServicoRepository>();
        var servico = CreateServico(descricao: "Revisao eletrica", valorUnitario: 350m);
        var servicosConcluidos = new[]
        {
            CreateServicoDaOrdemDeServicoConcluido(servico.Id, 30),
            CreateServicoDaOrdemDeServicoConcluido(servico.Id, 45),
            CreateServicoDaOrdemDeServicoConcluido(servico.Id, 60)
        };

        _ = servicoRepositoryMock
            .Setup(x => x.GetByIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Servico>.Success(servico));

        _ = servicoDaOrdemRepositoryMock
            .Setup(x => x.GetConcluidosByServicoIdAsync(servico.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyCollection<ServicoDaOrdemDeServico>>.Success(servicosConcluidos));

        var useCase = new VerificarTempoMedioServicoUseCase(servicoRepositoryMock.Object, servicoDaOrdemRepositoryMock.Object);

        var result = await useCase.ExecuteAsync(new VerificarTempoMedioServicoCommand { ServicoId = servico.Id });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.Not.Null);
            Assert.That(result.Value!.ServicoId, Is.EqualTo(servico.Id));
            Assert.That(result.Value.QuantidadeExecucoes, Is.EqualTo(3));
            Assert.That(result.Value.TempoMedioMinutos, Is.EqualTo(45m));
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

    private static ServicoDaOrdemDeServico CreateServicoDaOrdemDeServicoConcluido(Guid servicoId, int tempoGastoMinutos)
    {
        var snapshot = new ServicoDaOrdemDeServicoSnapshot(
            Guid.NewGuid(),
            Guid.NewGuid(),
            servicoId,
            "Servico concluido",
            100m,
            1,
            tempoGastoMinutos,
            true);

        var servicoDaOrdemResult = ServicoDaOrdemDeServico.Rehydrate(snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(servicoDaOrdemResult.IsSuccess, Is.True);
            Assert.That(servicoDaOrdemResult.Value, Is.Not.Null);
        });

        return servicoDaOrdemResult.Value!;
    }
}

