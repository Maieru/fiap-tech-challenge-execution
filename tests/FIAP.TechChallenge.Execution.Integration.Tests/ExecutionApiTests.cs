using System.Net;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.DependencyInjection;
using FIAP.TechChallenge.Execution.Infrastructure.Messaging;
using FIAP.TechChallenge.Execution.Domain.Entities;
using Confluent.Kafka;
using System.Text;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

[TestFixture, NonParallelizable, Category("Integration")]
public sealed class ExecutionApiTests
{
    private ExecutionHost _host = null!;
    [OneTimeSetUp] public async Task Start() { _host = new(); await _host.StartAsync(); }
    [OneTimeTearDown] public async Task Stop() { if (_host is not null) await _host.DisposeAsync(); }

    [Test]
    public async Task Catalogo_ShouldPreserveSnapshotsAndSoftDelete()
    {
        var service = await _host.CreateServicoAsync(); var part = await _host.CreatePecaAsync(); var order = await _host.CreateOrderAsync();
        var serviceId = service.GetProperty("id").GetGuid(); var partId = part.GetProperty("id").GetGuid();
        Assert.That((await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var added = await _host.PostCreatedAsync($"/ordens/{order}/servicos", new { ServicoId = serviceId, Quantidade = 2 });
        await _host.PostCreatedAsync($"/ordens/{order}/pecas-insumos", new { PecaInsumoId = partId, Quantidade = 3 }, "reserve-" + order);
        var updated = await _host.RequestAsync(HttpMethod.Put, "/servicos/" + serviceId, new { Id = Guid.NewGuid(), Descricao = "Novo nome", ValorUnitario = 999m });
        var updatePart = await _host.RequestAsync(HttpMethod.Put, "/pecas-insumos/" + partId,
            new { Id = Guid.NewGuid(), Nome = "Filtro atualizado", Codigo = part.GetProperty("codigo").GetString(), PrecoUnitario = 999m });
        var deletedService = await _host.RequestAsync(HttpMethod.Delete, "/servicos/" + serviceId);
        var deletedPart = await _host.RequestAsync(HttpMethod.Delete, "/pecas-insumos/" + partId);
        var detail = await _host.GetAsync("/ordens/" + order);
        Assert.Multiple(() =>
        {
            Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.OK)); Assert.That(updatePart.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(deletedService.StatusCode, Is.EqualTo(HttpStatusCode.NoContent)); Assert.That(deletedPart.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(detail.GetProperty("servicos")[0].GetProperty("valorUnitario").GetDecimal(), Is.EqualTo(35.50m));
            Assert.That(detail.GetProperty("pecasInsumos")[0].GetProperty("precoUnitario").GetDecimal(), Is.EqualTo(12.25m));
        });
        Assert.Multiple(() =>
        {
            Assert.That(_host.Database.GetCollection<ServicoDocument>("servicos").Find(x => x.Id == serviceId).Single().Ativo, Is.False);
            Assert.That(_host.Database.GetCollection<PecaDocument>("pecas").Find(x => x.Id == partId).Single().Ativo, Is.False);
        });
        foreach (var path in new[] { "/servicos/" + serviceId, "/pecas-insumos/" + partId })
        {
            Assert.That((await _host.RequestAsync(HttpMethod.Get, path)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That((await _host.RequestAsync(HttpMethod.Delete, path)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        }
        Assert.That(added.GetProperty("ordemServicoId").GetGuid(), Is.EqualTo(order));
    }

    [Test]
    public async Task Stock_ShouldReplayTheSameOperationAndRejectChangedPayload()
    {
        var part = await _host.CreatePecaAsync(); var partId = part.GetProperty("id").GetGuid(); var key = Guid.NewGuid().ToString();
        var first = await _host.RequestAsync(HttpMethod.Put, $"/pecas-insumos/{partId}/entrada-estoque", new { Quantidade = 4 }, key);
        var second = await _host.RequestAsync(HttpMethod.Put, $"/pecas-insumos/{partId}/entrada-estoque", new { Quantidade = 4 }, key);
        var mismatch = await _host.RequestAsync(HttpMethod.Put, $"/pecas-insumos/{partId}/entrada-estoque", new { Quantidade = 5 }, key);
        var missing = await _host.RequestAsync(HttpMethod.Put, $"/pecas-insumos/{partId}/entrada-estoque", new { Quantidade = 4 });
        Assert.Multiple(() =>
        {
            Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK)); Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(mismatch.StatusCode, Is.EqualTo(HttpStatusCode.Conflict)); Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        });
        var stock = await _host.GetAsync("/pecas-insumos/" + partId);
        Assert.That(stock.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(14));
    }

    [Test]
    public async Task Flow_ShouldRequireAuthorizationAndAllServicesCompleted()
    {
        var service = await _host.CreateServicoAsync(); var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        var item = await _host.PostCreatedAsync($"/ordens/{order}/servicos", new { ServicoId = service.GetProperty("id").GetGuid(), Quantidade = 1 });
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/concluir-diagnostico");
        var completePath = $"/ordens/{order}/servicos/{item.GetProperty("id").GetGuid()}/concluir";
        Assert.That((await _host.RequestAsync(HttpMethod.Post, completePath, new { TempoGastoMinutos = 10 })).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await _host.SendAsync(ExecutionHost.Envelope("AutorizacaoExecucaoSolicitada", new(order, 2, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator")));
        await _host.WaitStateAsync(order, 4);
        Assert.That((await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/finalizar")).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var done = await _host.RequestAsync(HttpMethod.Post, completePath, new { TempoGastoMinutos = 17 });
        var finalized = await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/finalizar");
        var average = await _host.GetAsync($"/servicos/{service.GetProperty("id").GetGuid()}/tempo-medio");
        Assert.Multiple(() =>
        {
            Assert.That(done.StatusCode, Is.EqualTo(HttpStatusCode.OK)); Assert.That(finalized.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(average.GetProperty("tempoMedioMinutos").GetDecimal(), Is.EqualTo(17));
        });
    }

    [Test]
    public async Task Reservation_ShouldBeIdempotentAndRejectOverselling()
    {
        var part = await _host.CreatePecaAsync(3); var partId = part.GetProperty("id").GetGuid(); var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        var path = $"/ordens/{order}/pecas-insumos"; var key = Guid.NewGuid().ToString();
        var first = await _host.PostCreatedAsync(path, new { PecaInsumoId = partId, Quantidade = 2 }, key);
        var replay = await _host.PostCreatedAsync(path, new { PecaInsumoId = partId, Quantidade = 2 }, key);
        var changed = await _host.RequestAsync(HttpMethod.Post, path, new { PecaInsumoId = partId, Quantidade = 1 }, key);
        var excessive = await _host.RequestAsync(HttpMethod.Post, path, new { PecaInsumoId = partId, Quantidade = 2 }, "other-" + key);
        var stock = await _host.GetAsync("/pecas-insumos/" + partId);
        Assert.Multiple(() =>
        {
            Assert.That(first.GetProperty("id").GetGuid(), Is.EqualTo(replay.GetProperty("id").GetGuid()));
            Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict)); Assert.That(excessive.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(stock.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Stock_ShouldNotBecomeNegativeWhenOrdersCompete()
    {
        var part = await _host.CreatePecaAsync(5); var id = part.GetProperty("id").GetGuid();
        var a = await _host.CreateOrderAsync(); var b = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{a}/iniciar-diagnostico");
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{b}/iniciar-diagnostico");
        var results = await Task.WhenAll(new[] { a, b }.Select(order => _host.RequestAsync(HttpMethod.Post,
            $"/ordens/{order}/pecas-insumos", new { PecaInsumoId = id, Quantidade = 4 }, order.ToString())));
        var stock = await _host.GetAsync("/pecas-insumos/" + id);
        Assert.Multiple(() =>
        {
            Assert.That(results.Count(x => x.StatusCode == HttpStatusCode.Created), Is.EqualTo(1));
            Assert.That(results.Count(x => x.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(1));
            Assert.That(stock.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Cancellation_ShouldRestoreInactivePartsOnceAndDeduplicateMessages()
    {
        var part = await _host.CreatePecaAsync(); var id = part.GetProperty("id").GetGuid(); var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        await _host.PostCreatedAsync($"/ordens/{order}/pecas-insumos", new { PecaInsumoId = id, Quantidade = 3 }, order.ToString());
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/concluir-diagnostico");
        await _host.RequestAsync(HttpMethod.Delete, "/pecas-insumos/" + id);
        var message = ExecutionHost.Envelope("CancelamentoSolicitado", new(order, 2, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator"));
        await _host.SendAsync(message); await _host.WaitStateAsync(order, 7);
        await _host.SendAsync(message); await _host.SendAsync(message with { MessageId = Guid.NewGuid() });
        await ExecutionHost.WaitAsync(async () => await _host.Database.GetCollection<InboxDocument>("inbox").CountDocumentsAsync(x => x.Id == message.MessageId) == 1);
        var row = await _host.Database.GetCollection<PecaDocument>("pecas").Find(x => x.Id == id).SingleAsync();
        var reservations = await _host.Database.GetCollection<ReservaDocument>("reservas").Find(x => x.OrdemServicoId == order).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(row.Ativo, Is.False); Assert.That(row.QuantidadeEstoque, Is.EqualTo(10));
            Assert.That(reservations.Count, Is.EqualTo(1)); Assert.That(reservations.All(x => x.Devolvida), Is.True);
        });
    }

    [Test]
    public async Task Reservation_ShouldRollbackStockWhenItsDocumentCannotBeStored()
    {
        var part = await _host.CreatePecaAsync(); var id = part.GetProperty("id").GetGuid(); var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        await SetValidator("reservas", new BsonDocument("impossibleField", new BsonDocument("$exists", true)));
        HttpResponseMessage response;
        try { response = await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/pecas-insumos", new { PecaInsumoId = id, Quantidade = 3 }, order.ToString()); }
        finally { await SetValidator("reservas", new BsonDocument()); }
        var row = await _host.GetAsync("/pecas-insumos/" + id); var detail = await _host.GetAsync("/ordens/" + order);
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(row.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(10)); Assert.That(detail.GetProperty("pecasInsumos").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public async Task Cancellation_ShouldRollbackStockInboxAndExecutionWhenOutboxFails()
    {
        var part = await _host.CreatePecaAsync(); var id = part.GetProperty("id").GetGuid(); var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        await _host.PostCreatedAsync($"/ordens/{order}/pecas-insumos", new { PecaInsumoId = id, Quantidade = 3 }, order.ToString());
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/concluir-diagnostico");
        var message = ExecutionHost.Envelope("CancelamentoSolicitado", new(order, 2, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator"));
        await SetValidator("outbox", new BsonDocument("impossibleField", new BsonDocument("$exists", true)));
        try
        {
            await using var scope = _host.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<IntegrationContext>().Incoming = message;
            Assert.ThrowsAsync<MongoWriteException>(async () => await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler>().ApplyAsync(message, default));
        }
        finally { await SetValidator("outbox", new BsonDocument()); }
        var row = await _host.GetAsync("/pecas-insumos/" + id); var detail = await _host.GetAsync("/ordens/" + order);
        var savedInbox = await _host.Database.GetCollection<InboxDocument>("inbox").CountDocumentsAsync(x => x.Id == message.MessageId);
        Assert.Multiple(() =>
        {
            Assert.That(row.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(7)); Assert.That(detail.GetProperty("estado").GetInt32(), Is.EqualTo(3));
            Assert.That(savedInbox, Is.Zero);
        });
        await _host.SendAsync(message); await _host.WaitStateAsync(order, 7);
    }

    [Test]
    public async Task Outbox_ShouldRecoverAfterKafkaRestartWithoutLosingTheChange()
    {
        var order = await _host.CreateOrderAsync(); await _host.Kafka.StopAsync();
        try
        {
            var response = await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
            var stored = await _host.Database.GetCollection<OutboxDocument>("outbox").Find(x => x.OrdemServicoId == order).SingleAsync();
            var live = await _host.Client.GetAsync(ExecutionHost.Root + "/health/live");
            Assert.Multiple(() =>
            {
                Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK)); Assert.That(stored.Published, Is.False);
                Assert.That(live.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            });
        }
        finally { await _host.Kafka.StartAsync(); }
        await ExecutionHost.WaitAsync(async () => await _host.Database.GetCollection<OutboxDocument>("outbox").CountDocumentsAsync(x => x.OrdemServicoId == order && !x.Published) == 0);
    }

    [TestCase("GET", "/servicos")][TestCase("POST", "/servicos")][TestCase("GET", "/pecas-insumos")][TestCase("POST", "/pecas-insumos")]
    [TestCase("GET", "/ordens")][TestCase("GET", "/servicos/00000000-0000-0000-0000-000000000001")]
    [TestCase("PUT", "/servicos/00000000-0000-0000-0000-000000000001")][TestCase("DELETE", "/servicos/00000000-0000-0000-0000-000000000001")]
    [TestCase("GET", "/servicos/00000000-0000-0000-0000-000000000001/tempo-medio")]
    [TestCase("GET", "/pecas-insumos/00000000-0000-0000-0000-000000000001")]
    [TestCase("PUT", "/pecas-insumos/00000000-0000-0000-0000-000000000001")][TestCase("DELETE", "/pecas-insumos/00000000-0000-0000-0000-000000000001")]
    [TestCase("PUT", "/pecas-insumos/00000000-0000-0000-0000-000000000001/entrada-estoque")]
    [TestCase("GET", "/ordens/00000000-0000-0000-0000-000000000001")]
    [TestCase("POST", "/ordens/00000000-0000-0000-0000-000000000001/iniciar-diagnostico")]
    [TestCase("POST", "/ordens/00000000-0000-0000-0000-000000000001/concluir-diagnostico")]
    [TestCase("POST", "/ordens/00000000-0000-0000-0000-000000000001/servicos")]
    [TestCase("POST", "/ordens/00000000-0000-0000-0000-000000000001/pecas-insumos")]
    [TestCase("POST", "/ordens/00000000-0000-0000-0000-000000000001/servicos/00000000-0000-0000-0000-000000000002/concluir")]
    [TestCase("POST", "/ordens/00000000-0000-0000-0000-000000000001/finalizar")]
    public async Task Operations_ShouldRejectAnonymousRequests(string method, string path)
    {
        _host.Client.DefaultRequestHeaders.Authorization = null;
        try { Assert.That((await _host.RequestAsync(new HttpMethod(method), path, new { })).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized)); }
        finally { _host.Authenticate(); }
    }

    [Test]
    public async Task Validation_ShouldKeepStoredValuesAndCheckPaginationFiltersAndUniqueness()
    {
        var service = await _host.CreateServicoAsync(); var part = await _host.CreatePecaAsync();
        var serviceId = service.GetProperty("id").GetGuid(); var partId = part.GetProperty("id").GetGuid();
        var invalidService = await _host.RequestAsync(HttpMethod.Put, "/servicos/" + serviceId, new { Descricao = "", ValorUnitario = 10m });
        var invalidPart = await _host.RequestAsync(HttpMethod.Put, "/pecas-insumos/" + partId, new { Nome = "x", Codigo = "C1", PrecoUnitario = -1m });
        var missing = await _host.RequestAsync(HttpMethod.Get, "/ordens/" + Guid.NewGuid());
        var badPage = await _host.RequestAsync(HttpMethod.Get, "/ordens?pageNumber=0");
        var emptyAverage = await _host.GetAsync($"/servicos/{serviceId}/tempo-medio");
        var byCode = await _host.GetAsync("/pecas-insumos?codigo=" + part.GetProperty("codigo").GetString());
        var services = await _host.GetAsync("/servicos?pageSize=1"); var parts = await _host.GetAsync("/pecas-insumos?pageSize=1");
        var fila = await _host.GetAsync("/ordens?estado=1&pageSize=1");
        Assert.Multiple(() =>
        {
            Assert.That(invalidService.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest)); Assert.That(invalidPart.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound)); Assert.That(badPage.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(byCode.GetProperty("id").GetGuid(), Is.EqualTo(partId)); Assert.That(emptyAverage.GetProperty("quantidadeExecucoes").GetInt32(), Is.Zero);
            Assert.That(services.GetProperty("servicos").GetArrayLength(), Is.EqualTo(1)); Assert.That(parts.GetProperty("pecasInsumos").GetArrayLength(), Is.EqualTo(1));
            Assert.That(fila.GetProperty("items").GetArrayLength(), Is.LessThanOrEqualTo(1));
        });
        Assert.That((await _host.GetAsync("/servicos/" + serviceId)).GetProperty("valorUnitario").GetDecimal(), Is.EqualTo(35.50m));
    }

    [Test]
    public async Task Dlq_ShouldPreserveTheEnvelopeAndReplayWithoutRegressingTheOrder()
    {
        var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/concluir-diagnostico");
        var gap = ExecutionHost.Envelope("AutorizacaoExecucaoSolicitada", new(order, 3, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator"));
        await _host.SendAsync(gap);
        var dead = ReadDeadLetter(value => value.Contains(gap.MessageId.ToString()));
        Assert.Multiple(() =>
        {
            Assert.That(dead.Message.Key, Is.EqualTo(order.ToString()));
            Assert.That(dead.Message.Value, Is.EqualTo(IntegrationContext.Serialize(gap)));
            Assert.That(Encoding.UTF8.GetString(dead.Message.Headers.GetLastBytes("original-topic")), Is.EqualTo("fiap.os.events.v1"));
        });
        await _host.SendAsync(ExecutionHost.Envelope("CancelamentoSolicitado", new(order, 2, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator")));
        await _host.WaitStateAsync(order, 7);
        await _host.SendRawAsync("fiap.os.events.v1", dead.Message.Key, dead.Message.Value);
        await ExecutionHost.WaitAsync(async () => await _host.Database.GetCollection<InboxDocument>("inbox").CountDocumentsAsync(x => x.Id == gap.MessageId) == 1);
        var current = await _host.GetAsync("/ordens/" + order);
        var refusal = await _host.Database.GetCollection<OutboxDocument>("outbox").Find(x => x.OrdemServicoId == order && x.Sequence == 4).SingleAsync();
        Assert.Multiple(() => { Assert.That(current.GetProperty("estado").GetInt32(), Is.EqualTo(7)); Assert.That(refusal.Body, Does.Contain("SolicitacaoRecusada")); });
    }

    [Test]
    public async Task Consumer_ShouldDeduplicatePersistedChangesAfterRestartBeforeOffsetConfirmation()
    {
        var part = await _host.CreatePecaAsync(); var partId = part.GetProperty("id").GetGuid(); var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        await _host.PostCreatedAsync($"/ordens/{order}/pecas-insumos", new { PecaInsumoId = partId, Quantidade = 3 }, order.ToString());
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/concluir-diagnostico");
        var message = ExecutionHost.Envelope("CancelamentoSolicitado", new(order, 2, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator"));
        // Persist the handler transaction without committing a Kafka offset, then restart and redeliver.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<IntegrationContext>().Incoming = message;
            await scope.ServiceProvider.GetRequiredService<IIntegrationEventHandler>().ApplyAsync(message, default);
        }
        await _host.RestartApiAsync(); await _host.SendAsync(message);
        var repeated = message with { MessageId = Guid.NewGuid() }; await _host.SendAsync(repeated);
        await ExecutionHost.WaitAsync(async () => await _host.Database.GetCollection<InboxDocument>("inbox").CountDocumentsAsync(x => x.Id == repeated.MessageId) == 1);
        var stock = await _host.GetAsync("/pecas-insumos/" + partId);
        Assert.That(stock.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(10));
    }

    [TestCase(false)][TestCase(true)]
    public async Task Consumer_ShouldDeadLetterMalformedOrUnsupportedEnvelopes(bool unsupportedVersion)
    {
        var order = Guid.NewGuid(); var message = ExecutionHost.Envelope("OrdemCriada", new(order, 1, Guid.NewGuid(), Guid.NewGuid(), "Motor com falha"));
        var body = unsupportedVersion ? IntegrationContext.Serialize(message with { Version = 999 }) : "{malformed-" + message.MessageId;
        await _host.SendRawAsync("fiap.os.events.v1", order.ToString(), body);
        var dead = ReadDeadLetter(value => value == body);
        Assert.That(dead.Message.Value, Is.EqualTo(body));
    }

    private ConsumeResult<string, string> ReadDeadLetter(Func<string, bool> matches)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig { BootstrapServers = _host.BootstrapServers,
            GroupId = "dlq-test-" + Guid.NewGuid(), AutoOffsetReset = AutoOffsetReset.Earliest, EnableAutoCommit = false }).Build();
        consumer.Subscribe("fiap.os.events.v1.dlq"); var until = DateTime.UtcNow.AddSeconds(20);
        try
        {
            while (DateTime.UtcNow < until)
            {
                var value = consumer.Consume(TimeSpan.FromMilliseconds(200));
                if (value is not null && matches(value.Message.Value)) return value;
            }
            throw new AssertionException("Mensagem esperada nao encontrada na DLQ.");
        }
        finally { consumer.Close(); }
    }

    private async Task SetValidator(string collection, BsonDocument validator) =>
        await _host.Database.RunCommandAsync<BsonDocument>(new BsonDocument { { "collMod", collection }, { "validator", validator } });

    [Test]
    public async Task SameConcurrentStockCommand_ShouldReturnItsOriginalResultWithoutDuplicateCredit()
    {
        var part = await _host.CreatePecaAsync(); var id = part.GetProperty("id").GetGuid(); var key = Guid.NewGuid().ToString();
        var replies = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            _host.RequestAsync(HttpMethod.Put, $"/pecas-insumos/{id}/entrada-estoque", new { Quantidade = 2 }, key)));
        foreach (var response in replies) Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That((await _host.GetAsync("/pecas-insumos/" + id)).GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(12));
    }

    [Test]
    public async Task ActiveCodeUniqueness_ShouldAllowAReplacementWithoutRedirectingOldReservations()
    {
        var part = await _host.CreatePecaAsync(); var oldId = part.GetProperty("id").GetGuid(); var code = part.GetProperty("codigo").GetString()!;
        var order = await _host.CreateOrderAsync();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/iniciar-diagnostico");
        await _host.PostCreatedAsync($"/ordens/{order}/pecas-insumos", new { PecaInsumoId = oldId, Quantidade = 2 }, order.ToString());
        var duplicate = await _host.RequestAsync(HttpMethod.Post, "/pecas-insumos", new { Nome = "Filtro novo", Codigo = code.ToLowerInvariant(), PrecoUnitario = 20m, QuantidadeEstoque = 5 });
        Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await _host.RequestAsync(HttpMethod.Delete, "/pecas-insumos/" + oldId);
        var replacement = await _host.PostCreatedAsync("/pecas-insumos", new { Nome = "Filtro novo", Codigo = code.ToLowerInvariant(), PrecoUnitario = 20m, QuantidadeEstoque = 5 });
        var newId = replacement.GetProperty("id").GetGuid();
        await _host.RequestAsync(HttpMethod.Post, $"/ordens/{order}/concluir-diagnostico");
        await _host.SendAsync(ExecutionHost.Envelope("CancelamentoSolicitado", new(order, 2, SolicitacaoId: Guid.NewGuid(), OperadorId: "operator")));
        await _host.WaitStateAsync(order, 7);
        var original = await _host.Database.GetCollection<PecaDocument>("pecas").Find(x => x.Id == oldId).SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(newId, Is.Not.EqualTo(oldId)); Assert.That(original.Ativo, Is.False); Assert.That(original.QuantidadeEstoque, Is.EqualTo(10));
        });
        Assert.That((await _host.GetAsync("/pecas-insumos/" + newId)).GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(5));
    }
}
