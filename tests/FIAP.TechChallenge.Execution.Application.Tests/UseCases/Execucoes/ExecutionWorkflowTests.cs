using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarDiagnostico;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.RegistrarOrdemRecebida;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarExecucao;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.CancelarExecucao;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Maieru.FiapTechChallenge.Chassis;
using Moq;

namespace FIAP.TechChallenge.Execution.Application.Tests.UseCases.Execucoes;

public sealed class ExecutionWorkflowTests
{
    private static Execucao Order() => Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Motor falhando", DateTimeOffset.UtcNow).Value!;

    [TestCase("none")]
    [TestCase("save")]
    [TestCase("publish")]
    public async Task IniciarDiagnostico_ShouldSaveThenPublishThenCommit_OrAbortOnFailure(string failure)
    {
        var events = new List<string>();
        var order = Order();
        var repository = new Mock<IExecucaoRepository>();
        repository.Setup(x => x.GetByIdAsync(order.OrdemServicoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Execucao>.Success(order));
        repository.Setup(x => x.UpdateAsync(order, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                events.Add("save");
                return failure == "save" ? Task.FromException(new IOException("database write failed")) : Task.CompletedTask;
            });
        var transaction = new Mock<ITransaction>();
        transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => events.Add("commit")).Returns(Task.CompletedTask);
        transaction.Setup(x => x.DisposeAsync()).Callback(() => events.Add("dispose")).Returns(ValueTask.CompletedTask);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.BeginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var messaging = new Mock<IMessagingService>();
        messaging.Setup(x => x.PublishAsync(ExecutionMessaging.Topic, order.OrdemServicoId.ToString("D"),
            It.IsAny<MessageEnvelope<DiagnosticoIniciado>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                events.Add("publish");
                return failure == "publish" ? Task.FromException(new IOException("delivery failed")) : Task.CompletedTask;
            });
        var useCase = new IniciarDiagnosticoUseCase(repository.Object, unitOfWork.Object, messaging.Object, new MessageContext(), TimeProvider.System);
        if (failure != "none")
        {
            Assert.ThrowsAsync<IOException>(() => useCase.ExecuteAsync(new(order.OrdemServicoId)));
            Assert.That(events, Is.EqualTo(failure == "save" ? new[] { "save", "dispose" } : new[] { "save", "publish", "dispose" }));
        }
        else
        {
            var result = await useCase.ExecuteAsync(new(order.OrdemServicoId));
            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(events, Is.EqualTo(new[] { "save", "publish", "commit", "dispose" }));
            });
        }
    }

    [Test]
    public async Task Registrar_ShouldReturnSuccessWithoutRepositoryWrites_WhenMessageAlreadyProcessed()
    {
        var messageId = Guid.NewGuid();
        var repository = new Mock<IExecucaoRepository>(MockBehavior.Strict);
        var processed = new Mock<IProcessedMessageRepository>();
        processed.Setup(x => x.ExistsAsync(messageId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var transaction = new Mock<ITransaction>();
        transaction.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.BeginAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var useCase = new RegistrarOrdemRecebidaUseCase(repository.Object, unitOfWork.Object, processed.Object,
            new MessageContext { IncomingMessageId = messageId }, TimeProvider.System);
        var result = await useCase.ExecuteAsync(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Falha"));
        Assert.That(result.IsSuccess, Is.True);
        repository.VerifyNoOtherCalls();
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
