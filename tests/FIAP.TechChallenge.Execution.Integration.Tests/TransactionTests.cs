using System.Net;
using System.Net.Http.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Application.UseCases.Execucoes.IniciarDiagnostico;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using FIAP.TechChallenge.OS.Application.Messaging;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class TransactionTests : IntegrationTest
{
    [Test]
    public async Task Stock_ShouldAllowOnlyOneConcurrentDebit_AndNeverBecomeNegative()
    {
        var part = await PartAsync(1);
        var first = await ReceiveOrderAsync();
        var second = await ReceiveOrderAsync();
        await PutAsync($"ordens/{first}/iniciar-diagnostico");
        await PutAsync($"ordens/{second}/iniciar-diagnostico");
        var responses = await Task.WhenAll(
            Client.PostAsJsonAsync($"{Prefix}/ordens/{first}/addpecainsumo", new { pecaInsumoId = part, quantidade = 1 }),
            Client.PostAsJsonAsync($"{Prefix}/ordens/{second}/addpecainsumo", new { pecaInsumoId = part, quantidade = 1 }));
        try
        {
            var orders = await Task.WhenAll(GetOrderAsync(first), GetOrderAsync(second));
            Assert.Multiple(() =>
            {
                Assert.That(responses.Count(x => x.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
                Assert.That(responses.Count(x => x.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest), Is.EqualTo(1));
                Assert.That(orders.Sum(x => x.Execucao.PecasInsumos.Count), Is.EqualTo(1));
            });
            Assert.That(await StockAsync(part), Is.Zero);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Test]
    public async Task Repository_ShouldRollbackAllWrites_WhenTransactionIsNotCommitted()
    {
        var part = await PartAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPecaInsumoRepository>();
            await using var transaction = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginAsync();
            var value = (await repository.GetByIdAsync(part)).Value!;
            _ = value.RemoveEstoque(5);
            await repository.UpdateAsync(value);
        }
        Assert.That(await StockAsync(part), Is.EqualTo(10));
    }

    [Test]
    public async Task Repository_ShouldNotResurrectService_WhenStaleUpdateRacesWithSoftDelete()
    {
        var id = await ServiceAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IServicoRepository>();
        var value = (await repository.GetByIdAsync(id)).Value!;
        using var deleted = await Client.DeleteAsync($"{Prefix}/servicos/{id}");
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        _ = value.UpdateDescricao("Tentativa antiga");
        Assert.ThrowsAsync<ConcurrencyException>(() => repository.UpdateAsync(value));
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var row = await database.GetCollection<ServicoDocument>("servicos").Find(x => x.Id == id).SingleAsync();
        Assert.That(row.Ativo, Is.False);
    }

    [Test]
    public async Task Cancellation_ShouldReturnStockEvenAfterPartWasInactivated()
    {
        var part = await PartAsync();
        var id = await ReceiveOrderAsync();
        await PutAsync($"ordens/{id}/iniciar-diagnostico");
        using var added = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addpecainsumo", new { pecaInsumoId = part, quantidade = 3 });
        _ = await ReadAsync(added, HttpStatusCode.Created);
        using var deleted = await Client.DeleteAsync($"{Prefix}/pecas-insumos/{part}");
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        await PutAsync($"ordens/{id}/solicitar-aprovacao");
        await SendAsync(id, CancelamentoSolicitado.Type, new CancelamentoSolicitado(id));
        await WaitOrderAsync(id, StatusExecucao.Cancelada);
        var database = Factory.Services.GetRequiredService<IMongoDatabase>();
        var row = await database.GetCollection<PecaDocument>("pecas_insumos").Find(x => x.Id == part).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(row.QuantidadeEstoque, Is.EqualTo(10));
            Assert.That(row.Ativo, Is.False);
        });
    }

    [Test]
    public async Task Publication_ShouldRemainDelivered_WhenDatabaseCommitFails_AndDatabaseChangesAbort()
    {
        var id = await ReceiveOrderAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var useCase = new IniciarDiagnosticoUseCase(
            scope.ServiceProvider.GetRequiredService<IExecucaoRepository>(),
            new FailCommitUnitOfWork(scope.ServiceProvider.GetRequiredService<IUnitOfWork>()),
            scope.ServiceProvider.GetRequiredService<IMessagingService>(), new MessageContext(), TimeProvider.System);
        Assert.ThrowsAsync<IOException>(() => useCase.ExecuteAsync(new(id)));
        var delivered = ReadEvent(id, DiagnosticoIniciado.Type);
        var order = await GetOrderAsync(id);
        Assert.Multiple(() =>
        {
            Assert.That(delivered.Payload.GetProperty("ordemServicoId").GetGuid(), Is.EqualTo(id));
            Assert.That(order.Execucao.Status, Is.EqualTo(StatusExecucao.Recebida));
        });
    }

    [Test]
    public async Task Consumer_ShouldStopWithoutAdvancingOffset_WhenMessageTypeIsUnknown()
    {
        var previousExitCode = Environment.ExitCode;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = Factory.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped
            .Register(() => stopped.TrySetResult());
        try
        {
            await SendAsync(Guid.NewGuid(), "os.unsupported", new { ordemServicoId = Guid.NewGuid() });
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using var probe = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = TestEnvironment.Kafka.GetBootstrapAddress(), GroupId = GroupId,
                EnableAutoCommit = false, BrokerAddressFamily = BrokerAddressFamily.V4
            }).Build();
            var offset = probe.Committed([new TopicPartition(InputTopic, 0)], TimeSpan.FromSeconds(10)).Single().Offset;
            Assert.Multiple(() =>
            {
                Assert.That(offset.Value, Is.LessThan(0));
                Assert.That(Environment.ExitCode, Is.EqualTo(1));
            });
        }
        finally { Environment.ExitCode = previousExitCode; }
    }

    [Test]
    public async Task Cancellation_ShouldAbortStockStatusAndDeduplication_WhenKafkaPublicationFails()
    {
        var part = await PartAsync();
        var id = await ReceiveOrderAsync();
        await PutAsync($"ordens/{id}/iniciar-diagnostico");
        using var added = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addpecainsumo", new { pecaInsumoId = part, quantidade = 3 });
        _ = await ReadAsync(added, HttpStatusCode.Created);
        await PutAsync($"ordens/{id}/solicitar-aprovacao");
        using var admin = TestEnvironment.Admin();
        await admin.DeleteTopicsAsync([ExecutionMessaging.Topic]);
        var previousExitCode = Environment.ExitCode;
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = Factory.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopped
            .Register(() => stopped.TrySetResult());
        try
        {
            var messageId = Guid.NewGuid();
            var sent = await SendAsync(id, CancelamentoSolicitado.Type, new CancelamentoSolicitado(id), messageId);
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var database = Factory.Services.GetRequiredService<IMongoDatabase>();
            var row = await database.GetCollection<ExecucaoDocument>("execucoes").Find(x => x.Id == id).SingleAsync();
            var stock = await database.GetCollection<PecaDocument>("pecas_insumos").Find(x => x.Id == part).SingleAsync();
            var marker = await database.GetCollection<ProcessedMessageDocument>("mensagens_processadas")
                .Find(x => x.Id == $"oficina-execution-v1/{messageId:D}").AnyAsync();
            using var probe = new ConsumerBuilder<string, string>(new ConsumerConfig
            {
                BootstrapServers = TestEnvironment.Kafka.GetBootstrapAddress(), GroupId = GroupId,
                EnableAutoCommit = false, BrokerAddressFamily = BrokerAddressFamily.V4
            }).Build();
            var offset = probe.Committed([new TopicPartition(InputTopic, 0)], TimeSpan.FromSeconds(10)).Single().Offset;
            Assert.Multiple(() =>
            {
                Assert.That(row.Status, Is.EqualTo(StatusExecucao.AguardandoAprovacao));
                Assert.That(stock.QuantidadeEstoque, Is.EqualTo(7));
                Assert.That(marker, Is.False);
                Assert.That(offset.Value, Is.EqualTo(sent.Offset.Value));
            });
        }
        finally
        {
            Environment.ExitCode = previousExitCode;
            await admin.CreateTopicsAsync([new TopicSpecification { Name = ExecutionMessaging.Topic, NumPartitions = 1, ReplicationFactor = 1 }]);
        }
    }

    // Failure injection is restricted to this direct use-case test. HTTP/production DI is never replaced.
    private sealed class FailCommitUnitOfWork(IUnitOfWork inner) : IUnitOfWork
    {
        public async Task<ITransaction> BeginAsync(CancellationToken cancellationToken = default) =>
            new FailCommitTransaction(await inner.BeginAsync(cancellationToken));
    }
    private sealed class FailCommitTransaction(ITransaction inner) : ITransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => throw new IOException("Injected commit failure after broker confirmation.");
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
