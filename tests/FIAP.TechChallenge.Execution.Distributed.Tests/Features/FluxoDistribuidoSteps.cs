using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Reqnroll;

namespace FIAP.TechChallenge.Execution.Distributed.Tests.Features;

[Binding]
public sealed class FluxoDistribuidoSteps
{
    private HttpClient os = null!;
    private HttpClient execution = null!;
    private HttpClient billing = null!;
    private Guid chargeId;
    private string correlation = null!;
    private Guid customerId, vehicleId, orderId, serviceId, partId;
    private int historyCount;
    private bool authorized;
    private static int identity = 100000000;
    private DistributedEnvironment Environment => DistributedEnvironment.Current;

    [BeforeScenario]
    public void Start()
    {
        correlation = $"bdd-{Guid.NewGuid():N}";
        os = Environment.OsClient(correlation);
        execution = Environment.ExecutionClient(correlation);
        billing = Environment.BillingClient(correlation);
    }
    [AfterScenario]
    public void Stop() { os.Dispose(); execution.Dispose(); billing.Dispose(); }

    [Given("um cliente e um veículo ativos no OS")]
    public async Task Registrations()
    {
        var number = Interlocked.Increment(ref identity);
        var customer = await Post(os, "/api/os/v1/clientes", new { nome = "Cliente BDD", cpf = Cpf(number), telefone = "11987654321" });
        customerId = customer.GetProperty("id").GetGuid();
        var vehicle = await Post(os, "/api/os/v1/veiculos", new { clienteId = customerId, placa = $"BDD{number % 10000:D4}", marca = "Toyota", modelo = "Corolla", ano = 2024 });
        vehicleId = vehicle.GetProperty("id").GetGuid();
    }
    private async Task OpenOrder()
    {
        var order = await Post(os, "/api/os/v1/ordens", new { clienteId = customerId, veiculoId = vehicleId, descricaoProblema = "Motor com falha" });
        orderId = order.GetProperty("id").GetGuid();
    }
    [Given("uma ordem aberta no OS e recebida no Execution")]
    public async Task ReceivedOrder()
    {
        await Registrations();
        await OpenOrder();
        await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 1, nested: true);
    }
    [Given("um serviço e uma peça com cinco unidades cadastrados no Execution")]
    public async Task Catalog()
    {
        var service = await Post(execution, "/api/execution/v1/servicos", new { descricao = "Revisão", valorUnitario = 100.25m });
        serviceId = service.GetProperty("id").GetGuid();
        var part = await Post(execution, "/api/execution/v1/pecas-insumos", new
        { nome = "Filtro", codigo = $"FL-{Guid.NewGuid():N}", precoUnitario = 20.15m, quantidadeEstoque = 5 });
        partId = part.GetProperty("id").GetGuid();
    }
    [When("concluo o diagnóstico com um serviço e duas peças")]
    public async Task Diagnose()
    {
        await Put(execution, $"/api/execution/v1/ordens/{orderId}/iniciar-diagnostico");
        await Post(execution, $"/api/execution/v1/ordens/{orderId}/addservico", new { servicoId = serviceId, quantidade = 1 });
        await Post(execution, $"/api/execution/v1/ordens/{orderId}/addpecainsumo", new { pecaInsumoId = partId, quantidade = 2 });
        await Put(execution, $"/api/execution/v1/ordens/{orderId}/solicitar-aprovacao");
        await Wait(os, $"/api/os/v1/ordens/{orderId}", 3);
    }
    [Given("uma ordem com diagnóstico concluído e duas peças reservadas")]
    public async Task AwaitingOrder() { await ReceivedOrder(); await Catalog(); await Diagnose(); }

    [When("aprovo a execução pela API OS")]
    public async Task Approve()
    {
        await Put(os, $"/api/os/v1/ordens/{orderId}/aprovar-execucao", expected: HttpStatusCode.Accepted);
        await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 4, nested: true);
        await Wait(os, $"/api/os/v1/ordens/{orderId}", 4);
    }
    [When("concluo o serviço e finalizo a execução")]
    public async Task Complete()
    {
        var current = await execution.GetFromJsonAsync<JsonElement>($"/api/execution/v1/ordens/{orderId}");
        var itemId = current.GetProperty("execucao").GetProperty("servicos")[0].GetProperty("id").GetGuid();
        await Put(execution, $"/api/execution/v1/ordens/{orderId}/servicos/{itemId}/concluir", new { tempoGastoMinutos = 45 });
        await Put(execution, $"/api/execution/v1/ordens/{orderId}/finalizar");
    }
    [Then("as duas APIs apresentam a ordem finalizada com histórico e snapshots preservados")]
    public async Task Finalized()
    {
        var projected = await Wait(os, $"/api/os/v1/ordens/{orderId}", 5);
        var result = await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 5, nested: true);
        var history = await History();
        var stock = await Stock();
        Assert.Multiple(() =>
        {
            Assert.That(result.GetProperty("valorTotal").GetDecimal(), Is.EqualTo(140.55m));
            Assert.That(result.GetProperty("execucao").GetProperty("servicos")[0].GetProperty("descricao").GetString(), Is.EqualTo("Revisão"));
            Assert.That(projected.GetProperty("dataFinalizacao").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(history.GetArrayLength(), Is.EqualTo(7));
            Assert.That(history.EnumerateArray().All(x => x.GetProperty("correlationId").GetString() == correlation), Is.True);
            Assert.That(stock, Is.EqualTo(3));
        });
    }
    [Then("os três serviços apresentam o mesmo pagamento e orçamento")]
    public async Task PaymentConsistent()
    {
        var order = await Wait(os, $"/api/os/v1/ordens/{orderId}", 5);
        var executionOrder = await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 5, nested: true);
        var charge = await billing.GetFromJsonAsync<JsonElement>($"/api/billing/v1/ordens/{orderId}/cobranca");
        var page = await billing.GetFromJsonAsync<JsonElement>($"/api/billing/v1/cobrancas?ordemServicoId={orderId}");
        chargeId = charge.GetProperty("id").GetGuid();
        var payment = order.GetProperty("pagamento");
        Assert.Multiple(() =>
        {
            Assert.That(charge.GetProperty("ordemServicoId").GetGuid(), Is.EqualTo(orderId));
            Assert.That(charge.GetProperty("clienteId").GetGuid(), Is.EqualTo(customerId));
            Assert.That(charge.GetProperty("orcamentoId").GetGuid(), Is.EqualTo(order.GetProperty("orcamentoId").GetGuid()));
            Assert.That(charge.GetProperty("valor").GetDecimal(), Is.EqualTo(140.55m));
            Assert.That(charge.GetProperty("valor").GetDecimal(), Is.EqualTo(executionOrder.GetProperty("valorTotal").GetDecimal()));
            Assert.That(charge.GetProperty("valor").GetDecimal(), Is.EqualTo(payment.GetProperty("valor").GetDecimal()));
            Assert.That(charge.GetProperty("moeda").GetString(), Is.EqualTo("BRL"));
            Assert.That(charge.GetProperty("situacao").GetInt32(), Is.EqualTo(1));
            Assert.That(charge.GetProperty("gatewayPaymentId").GetString(), Is.EqualTo("mock-" + orderId.ToString("D")));
            Assert.That(payment.GetProperty("cobrancaId").GetGuid(), Is.EqualTo(chargeId));
            Assert.That(payment.GetProperty("gatewayPaymentId").GetString(), Is.EqualTo(charge.GetProperty("gatewayPaymentId").GetString()));
            Assert.That(payment.GetProperty("etapa").GetInt32(), Is.EqualTo(3));
            Assert.That(page.GetProperty("totalItems").GetInt32(), Is.EqualTo(1));
        });
    }

    [When("reentrego o pedido de pagamento, a confirmação e a autorização")]
    public async Task ReplayPayment()
    {
        await Environment.ReplayAsync(DistributedEnvironment.PaymentRequestTopic, "os.pagamento-solicitado", orderId, Environment.BillingGroup);
        await Environment.ReplayAsync(DistributedEnvironment.BillingTopic, "billing.pagamento-confirmado", orderId, Environment.OsGroup);
        await Environment.ReplayAsync(DistributedEnvironment.OsTopic, "os.execucao-autorizada", orderId, Environment.ExecutionGroup);
    }

    [Then("o pagamento e a ordem finalizada permanecem sem efeitos duplicados")]
    public async Task PaymentUnchanged()
    {
        var originalChargeId = chargeId;
        await PaymentConsistent();
        Assert.That(chargeId, Is.EqualTo(originalChargeId));
        await Finalized();
        // Each replay has committed its input offset; publications happen before those commits.
        Assert.Multiple(() =>
        {
            Assert.That(Environment.Messages(DistributedEnvironment.BillingTopic, orderId).Count(x => x.Type == "billing.pagamento-confirmado"), Is.EqualTo(2)); // original plus deliberate replay
            Assert.That(Environment.Messages(DistributedEnvironment.OsTopic, orderId).Count(x => x.Type == "os.execucao-autorizada"), Is.EqualTo(2)); // original plus deliberate replay
            Assert.That(Environment.Messages(DistributedEnvironment.ExecutionTopic, orderId).Count(x => x.Type == "execution.execucao-iniciada"), Is.EqualTo(1));
        });
        foreach (var pair in new[] { (os, "/api/os/v1/health/ready"), (billing, "/api/billing/v1/health/ready"), (execution, "/api/execution/v1/health/ready") })
        {
            using var response = await pair.Item1.GetAsync(pair.Item2);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }
        var snapshots = await Task.WhenAll(
            os.GetFromJsonAsync<JsonElement>($"/api/os/v1/ordens/{orderId}"),
            billing.GetFromJsonAsync<JsonElement>($"/api/billing/v1/ordens/{orderId}/cobranca"),
            execution.GetFromJsonAsync<JsonElement>($"/api/execution/v1/ordens/{orderId}"));
        var evidencePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artifacts", "distributed-evidence", $"{orderId:D}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(new
        {
            verifiedAtUtc = DateTimeOffset.UtcNow, correlationId = correlation, ordemServicoId = orderId,
            cobrancaId = chargeId, estoqueFinal = await Stock(), reentregasVerificadas = true,
            os = snapshots[0], billing = snapshots[1], execution = snapshots[2]
        }, new JsonSerializerOptions { WriteIndented = true }));
        TestContext.AddTestAttachment(evidencePath, "Estado das três APIs após conclusão e reentrega do pagamento");
        TestContext.WriteLine($"Evidência ponta a ponta: {evidencePath}");
    }

    [Then("as mensagens preservam correlação, causação e trace da autorização")]
    public void Trace()
    {
        var commands = Environment.Messages(DistributedEnvironment.OsTopic, orderId);
        var events = Environment.Messages(DistributedEnvironment.ExecutionTopic, orderId);
        var command = commands.Single(x => x.Type == "os.execucao-autorizada").Envelope;
        var started = events.Single(x => x.Type == "execution.execucao-iniciada").Envelope;
        var requested = Environment.Messages(DistributedEnvironment.PaymentRequestTopic, orderId).Single(x => x.Type == "os.pagamento-solicitado").Envelope;
        var confirmed = Environment.Messages(DistributedEnvironment.BillingTopic, orderId).Single(x => x.Type == "billing.pagamento-confirmado").Envelope;
        Assert.Multiple(() =>
        {
            Assert.That(confirmed.GetProperty("causationId").GetGuid(), Is.EqualTo(requested.GetProperty("messageId").GetGuid()));
            Assert.That(command.GetProperty("causationId").GetGuid(), Is.EqualTo(confirmed.GetProperty("messageId").GetGuid()));
            Assert.That(confirmed.GetProperty("correlationId").GetString(), Is.EqualTo(correlation));
            Assert.That(confirmed.GetProperty("traceParent").GetString()!.Split('-')[1], Is.EqualTo(requested.GetProperty("traceParent").GetString()!.Split('-')[1]));
            Assert.That(command.GetProperty("traceParent").GetString()!.Split('-')[1], Is.EqualTo(confirmed.GetProperty("traceParent").GetString()!.Split('-')[1]));
        });
        Assert.Multiple(() =>
        {
            Assert.That(command.GetProperty("correlationId").GetString(), Is.EqualTo(correlation));
            Assert.That(started.GetProperty("correlationId").GetString(), Is.EqualTo(correlation));
            Assert.That(started.GetProperty("causationId").GetGuid(), Is.EqualTo(command.GetProperty("messageId").GetGuid()));
            Assert.That(started.GetProperty("traceParent").GetString()!.Split('-')[1], Is.EqualTo(command.GetProperty("traceParent").GetString()!.Split('-')[1]));
        });
    }

    [When("solicito o cancelamento pela API OS")]
    public async Task Cancel() => await Put(os, $"/api/os/v1/ordens/{orderId}/cancelar", expected: HttpStatusCode.Accepted);
    [Then("as duas APIs apresentam a ordem cancelada e o estoque volta para cinco")]
    public async Task Cancelled()
    {
        await Wait(os, $"/api/os/v1/ordens/{orderId}", 7);
        await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 7, nested: true);
        var history = await History();
        historyCount = history.GetArrayLength();
        Assert.Multiple(() =>
        {
            Assert.That(historyCount, Is.EqualTo(5));
            Assert.That(history.EnumerateArray().Count(x => x.GetProperty("tipo").GetString() == "ExecucaoCancelada"), Is.EqualTo(1));
        });
        Assert.That(await Stock(), Is.EqualTo(5));
    }
    [When("reentrego o cancelamento e sua confirmação pelo Kafka")]
    public async Task Replay()
    {
        await Environment.ReplayAsync(DistributedEnvironment.OsTopic, "os.cancelamento-solicitado", orderId, Environment.ExecutionGroup);
        await Environment.ReplayAsync(DistributedEnvironment.ExecutionTopic, "execution.execucao-cancelada", orderId, Environment.OsGroup);
    }
    [Then("o estoque e o histórico continuam sem efeitos duplicados")]
    public async Task NoDuplicateEffects()
    {
        var history = await History();
        var stock = await Stock();
        Assert.Multiple(() =>
        {
            Assert.That(history.GetArrayLength(), Is.EqualTo(historyCount));
            Assert.That(stock, Is.EqualTo(5));
        });
    }

    [When("envio aprovação e cancelamento simultaneamente para o OS")]
    public async Task ConcurrentDecisions()
    {
        var replies = await Task.WhenAll(
            os.PutAsJsonAsync($"/api/os/v1/ordens/{orderId}/aprovar-execucao", new { }),
            os.PutAsJsonAsync($"/api/os/v1/ordens/{orderId}/cancelar", new { }));
        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(replies.Count(x => x.StatusCode == HttpStatusCode.Accepted), Is.EqualTo(1));
                Assert.That(replies.Count(x => x.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(1));
            });
            authorized = replies[0].StatusCode == HttpStatusCode.Accepted;
        }
        finally { foreach (var reply in replies) reply.Dispose(); }
    }
    [Then("apenas uma decisão é aceita e publicada")]
    public void OneDecision() => Assert.That(Environment.Messages(DistributedEnvironment.OsTopic, orderId).Concat(Environment.Messages("oficina.os.billing.requests.v1", orderId))
        .Count(x => x.Type is "os.pagamento-solicitado" or "os.cancelamento-solicitado"), Is.EqualTo(1));
    [Then("as duas APIs convergem para a decisão aceita")]
    public async Task Converged()
    {
        var expected = authorized ? 4 : 7;
        await Wait(os, $"/api/os/v1/ordens/{orderId}", expected);
        await Wait(execution, $"/api/execution/v1/ordens/{orderId}", expected, nested: true);
        Assert.That(await Stock(), Is.EqualTo(authorized ? 3 : 5));
    }

    [When("paro o Execution e abro uma ordem no OS")]
    public async Task StopAndOpen() { await Environment.StopExecutionAsync(); await OpenOrder(); }
    [Then("o OS confirma a abertura sem chamar a API Execution")]
    public async Task OpenWithoutExecution() => await Wait(os, $"/api/os/v1/ordens/{orderId}", 1);
    [When("reinicio o Execution")]
    public async Task Restart()
    {
        await Environment.StartExecutionAsync();
        execution.Dispose();
        execution = Environment.ExecutionClient(correlation);
    }
    [Then("a abertura pendente é recebida pelo Execution")]
    public async Task PendingReceived() => await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 1, nested: true);

    [When("paro o Billing e aprovo a ordem")]
    public async Task ApproveWithoutBilling() { await Environment.StopBillingAsync(); await Put(os, $"/api/os/v1/ordens/{orderId}/aprovar-execucao", expected: HttpStatusCode.Accepted); }
    [Then("a execução aguarda pagamento")]
    public async Task WaitingPayment() { var order = await Wait(os, $"/api/os/v1/ordens/{orderId}", 3); Assert.That(order.GetProperty("pagamento").GetProperty("etapa").GetInt32(), Is.EqualTo(0)); Assert.That(Environment.Messages(DistributedEnvironment.OsTopic,orderId).Any(x=>x.Type=="os.execucao-autorizada"), Is.False); }
    [When("reinicio o Billing")]
    public async Task RestartBilling() => await Environment.StartBillingAsync();
    [Then("o pagamento libera a execução")]
    public async Task PaymentStartsExecution() { await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 4, nested:true); await Wait(os, $"/api/os/v1/ordens/{orderId}",4); }
    [When("recebo uma recusa de pagamento pelo contrato Billing")]
    public async Task RejectPayment() => await Environment.RejectPaymentAsync(orderId);
    [Then("a compensação cancela a ordem preservando a aprovação")]
    public async Task Compensated()
    {
        var order = await Wait(os, $"/api/os/v1/ordens/{orderId}",7);
        await Wait(execution,$"/api/execution/v1/ordens/{orderId}",7,nested:true);
        Assert.Multiple(() => { Assert.That(order.GetProperty("decisao").GetInt32(),Is.EqualTo(1)); Assert.That(order.GetProperty("pagamento").GetProperty("etapa").GetInt32(),Is.EqualTo(5)); });
        Assert.That(await Stock(),Is.EqualTo(5));
        await Environment.ReplayAsync(DistributedEnvironment.OsTopic,"os.cancelamento-solicitado",orderId,Environment.ExecutionGroup);
        Assert.That(await Stock(),Is.EqualTo(5));
        await Environment.StartBillingAsync();
    }
    [Then("as três APIs exigem autenticação e aceitam o JWT emitido pelo OS")]
    public async Task OsTokenAcceptedByAllServices()
    {
        foreach (var entry in new[] { (os, "/api/os/v1/ordens"), (billing, "/api/billing/v1/cobrancas"), (execution, "/api/execution/v1/servicos") })
        {
            using var authenticated = await entry.Item1.GetAsync(entry.Item2);
            using var anonymous = new HttpClient { BaseAddress = entry.Item1.BaseAddress };
            using var denied = await anonymous.GetAsync(entry.Item2);
            anonymous.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
            using var invalid = await anonymous.GetAsync(entry.Item2);
            Assert.Multiple(() =>
            {
                Assert.That(authenticated.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(denied.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(invalid.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            });
        }
    }
    private string customerToken = null!;
    private string CustomerPath => $"/api/os/v1/ordens/acompanhamento/{orderId}?token={customerToken}";
    private async Task IssueCustomerAccess()
    {
        using var response = await os.PostAsJsonAsync($"/api/os/v1/ordens/{orderId}/acesso-cliente", new { });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        customerToken = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
    [When("o cliente consulta e aprova o orçamento pelo link privado")]
    public async Task CustomerApproves()
    {
        await IssueCustomerAccess();
        using var customer = new HttpClient { BaseAddress = os.BaseAddress };
        var order = await customer.GetFromJsonAsync<JsonElement>(CustomerPath);
        var budget = order.GetProperty("orcamento");
        Assert.Multiple(() =>
        {
            Assert.That(order.GetProperty("valorOrcamento").GetDecimal(), Is.EqualTo(140.55m));
            Assert.That(budget.GetProperty("servicos")[0].GetProperty("descricao").GetString(), Is.EqualTo("Revisão"));
            Assert.That(budget.GetProperty("pecasInsumos")[0].GetProperty("quantidade").GetInt32(), Is.EqualTo(2));
            Assert.That(order.TryGetProperty("clienteId", out _), Is.False);
        });
        await Put(customer, $"/api/os/v1/ordens/acompanhamento/{orderId}/aprovar-execucao?token={customerToken}", expected: HttpStatusCode.Accepted);
        await Put(customer, $"/api/os/v1/ordens/acompanhamento/{orderId}/aprovar-execucao?token={customerToken}", expected: HttpStatusCode.Accepted);
        await Put(customer, $"/api/os/v1/ordens/acompanhamento/{orderId}/cancelar?token={customerToken}", expected: HttpStatusCode.Conflict);
        await PaymentStartsExecution();
    }
    [When("registro a entrega administrativa duas vezes")]
    public async Task Deliver()
    {
        await Wait(os, $"/api/os/v1/ordens/{orderId}", 5);
        await Put(os, $"/api/os/v1/ordens/{orderId}/entregar");
        await Put(os, $"/api/os/v1/ordens/{orderId}/entregar");
        await Environment.ReplayAsync(DistributedEnvironment.ExecutionTopic, "execution.execucao-concluida", orderId, Environment.OsGroup);
    }
    [Then("o cliente acompanha a ordem entregue sem duplicar histórico")]
    public async Task CustomerSeesDelivery()
    {
        using var customer = new HttpClient { BaseAddress = os.BaseAddress };
        var order = await customer.GetFromJsonAsync<JsonElement>(CustomerPath);
        var history = await History();
        var executionOrder = await Wait(execution, $"/api/execution/v1/ordens/{orderId}", 5, nested: true);
        var charge = await billing.GetFromJsonAsync<JsonElement>($"/api/billing/v1/ordens/{orderId}/cobranca");
        Assert.Multiple(() =>
        {
            Assert.That(order.GetProperty("status").GetInt32(), Is.EqualTo(6));
            Assert.That(order.GetProperty("dataEntrega").GetDateTime(), Is.GreaterThanOrEqualTo(order.GetProperty("dataFinalizacao").GetDateTime()));
            Assert.That(history.GetArrayLength(), Is.EqualTo(8));
            Assert.That(history.EnumerateArray().Count(x => x.GetProperty("tipo").GetString() == "OrdemEntregue"), Is.EqualTo(1));
            Assert.That(charge.GetProperty("situacao").GetInt32(), Is.EqualTo(1));
            Assert.That(executionOrder.GetProperty("valorTotal").GetDecimal(), Is.EqualTo(140.55m));
        });
        Assert.That(await Stock(), Is.EqualTo(3));
    }
    [When("o cliente cancela pelo link privado")]
    public async Task CustomerCancels()
    {
        await IssueCustomerAccess();
        using var customer = new HttpClient { BaseAddress = os.BaseAddress };
        await Put(customer, $"/api/os/v1/ordens/acompanhamento/{orderId}/cancelar?token={customerToken}", expected: HttpStatusCode.Accepted);
        await Put(customer, $"/api/os/v1/ordens/acompanhamento/{orderId}/cancelar?token={customerToken}", expected: HttpStatusCode.Accepted);
        await Put(customer, $"/api/os/v1/ordens/acompanhamento/{orderId}/aprovar-execucao?token={customerToken}", expected: HttpStatusCode.Conflict);
    }

    private Task<JsonElement> History() => os.GetFromJsonAsync<JsonElement>($"/api/os/v1/ordens/{orderId}/historico");
    private async Task<int> Stock() => (await execution.GetFromJsonAsync<JsonElement>($"/api/execution/v1/pecas-insumos/{partId}"))
        .GetProperty("quantidadeEstoque").GetInt32();
    private static async Task<JsonElement> Post(HttpClient client, string route, object body)
    {
        using var response = await client.PostAsJsonAsync(route, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    private static async Task Put(HttpClient client, string route, object? body = null, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PutAsJsonAsync(route, body ?? new { });
        Assert.That(response.StatusCode, Is.EqualTo(expected), await response.Content.ReadAsStringAsync());
    }
    private static async Task<JsonElement> Wait(HttpClient client, string route, int status, bool nested = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (true)
        {
            using var response = await client.GetAsync(route, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
                var state = nested ? json.GetProperty("execucao") : json;
                if (state.GetProperty("status").GetInt32() == status) return json;
            }
            await Task.Delay(100, timeout.Token);
        }
    }
    private static string Cpf(int basis)
    {
        var digits = basis.ToString("D9").Select(x => x - '0').ToList();
        for (var length = 9; length <= 10; length++)
        {
            var sum = digits.Select((digit, index) => digit * (length + 1 - index)).Sum();
            var remainder = sum * 10 % 11;
            digits.Add(remainder == 10 ? 0 : remainder);
        }
        return string.Concat(digits);
    }
}
