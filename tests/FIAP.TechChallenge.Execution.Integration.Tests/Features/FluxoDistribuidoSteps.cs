using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using MongoDB.Driver;
using Reqnroll;

namespace FIAP.TechChallenge.Execution.Integration.Tests.Features;

[Binding, Scope(Tag = "EndToEnd")]
public sealed class FluxoDistribuidoSteps
{
    private DistributedHost _host = null!;
    private Guid _order, _customer, _vehicle, _part, _serviceItem;
    [BeforeScenario]
    public async Task Start()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OS_TEST_IMAGE")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EXECUTION_TEST_IMAGE")))
            Assert.Ignore("Construa as imagens e configure OS_TEST_IMAGE e EXECUTION_TEST_IMAGE para executar EndToEnd.");
        _host = new(); await _host.StartAsync();
    }
    [AfterScenario] public async Task Stop() { if (_host is not null) await _host.DisposeAsync(); }

    [Given("uma ordem aberta no OS e recebida pelo Execution")]
    public async Task Open()
    {
        _customer = (await _host.CreateAsync("/clientes", new { Nome = "Cliente BDD", Cpf = "52998224725", Email = "bdd@example.com", Telefone = "11987654321" })).GetProperty("id").GetGuid();
        _vehicle = (await _host.CreateAsync("/veiculos", new { ClienteId = _customer, Placa = "ABC1234", Marca = "Toyota", Modelo = "Corolla", Ano = 2024 })).GetProperty("id").GetGuid();
        _order = (await _host.CreateAsync("/ordens", new { ClienteId = _customer, VeiculoId = _vehicle, DescricaoProblema = "Motor com falha" })).GetProperty("id").GetGuid();
        await _host.Execution.WaitStateAsync(_order, 1);
    }
    [When("o diagnóstico inclui um serviço e uma peça")]
    public async Task Diagnose()
    {
        Assert.That((await _host.Execution.RequestAsync(HttpMethod.Post, $"/ordens/{_order}/iniciar-diagnostico")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var service = await _host.Execution.CreateServicoAsync(); _part = (await _host.Execution.CreatePecaAsync()).GetProperty("id").GetGuid();
        _serviceItem = (await _host.Execution.PostCreatedAsync($"/ordens/{_order}/servicos", new { ServicoId = service.GetProperty("id").GetGuid(), Quantidade = 1 })).GetProperty("id").GetGuid();
        await _host.Execution.PostCreatedAsync($"/ordens/{_order}/pecas-insumos", new { PecaInsumoId = _part, Quantidade = 3 }, _order.ToString());
        await _host.WaitOsAsync(_order, 2);
    }
    [When("o operador conclui o diagnóstico e autoriza administrativamente")]
    public async Task Authorize()
    {
        await FinishDiagnosis();
        var response = await _host.Os.PostAsync($"{DistributedHost.OsRoot}/ordens/{_order}/autorizar-execucao", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        await _host.Execution.WaitStateAsync(_order, 4); await _host.WaitOsAsync(_order, 4);
    }
    [When("a API de execução reinicia e conclui os serviços")]
    public async Task Finish()
    {
        await _host.Execution.RestartApiAsync();
        var response = await _host.Execution.RequestAsync(HttpMethod.Post, $"/ordens/{_order}/servicos/{_serviceItem}/concluir", new { TempoGastoMinutos = 17 });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _host.Execution.RequestAsync(HttpMethod.Post, $"/ordens/{_order}/finalizar")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await _host.WaitOsAsync(_order, 5);
    }
    [Then("a ordem fica finalizada no OS com histórico e auditoria")]
    public async Task CheckFinalized()
    {
        var order = await _host.OrderAsync(_order);
        var history = await _host.Os.GetFromJsonAsync<JsonElement>($"{DistributedHost.OsRoot}/ordens/{_order}/historico");
        var audit = history.EnumerateArray().Single(x => x.GetProperty("tipo").GetString() == "AutorizacaoExecucaoSolicitada");
        Assert.Multiple(() =>
        {
            Assert.That(order.GetProperty("status").GetInt32(), Is.EqualTo(5)); Assert.That(order.GetProperty("acaoPendente").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(history.GetArrayLength(), Is.EqualTo(6)); Assert.That(audit.GetProperty("detalhes").GetProperty("operadorId").GetString(), Is.EqualTo("operator"));
            Assert.That(audit.GetProperty("detalhes").GetProperty("origem").GetString(), Is.EqualTo("AdministrativaTemporaria"));
        });
    }
    [Then("inativar cliente e veículo não oculta a ordem finalizada")]
    public async Task Inactivate()
    {
        await _host.Os.DeleteAsync($"{DistributedHost.OsRoot}/clientes/{_customer}"); await _host.Os.DeleteAsync($"{DistributedHost.OsRoot}/veiculos/{_vehicle}");
        Assert.That((await _host.OrderAsync(_order)).GetProperty("status").GetInt32(), Is.EqualTo(5));
        Assert.That((await _host.Os.GetAsync($"{DistributedHost.OsRoot}/ordens/{_order}/historico")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
    [When("o operador conclui o diagnóstico e solicita o cancelamento")]
    public async Task Cancel()
    {
        await FinishDiagnosis();
        // Compensation must also work when the catalog row is inactive.
        await _host.Execution.RequestAsync(HttpMethod.Delete, "/pecas-insumos/" + _part);
        var response = await _host.Os.PostAsync($"{DistributedHost.OsRoot}/ordens/{_order}/cancelar", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted)); await _host.WaitOsAsync(_order, 7);
    }
    [Then("a ordem fica cancelada e a reserva é devolvida uma única vez")]
    public async Task CheckCancelled()
    {
        var part = await _host.Execution.Database.GetCollection<PecaDocument>("pecas").Find(x => x.Id == _part).SingleAsync();
        var reservations = await _host.Execution.Database.GetCollection<ReservaDocument>("reservas").Find(x => x.OrdemServicoId == _order).ToListAsync();
        var order = await _host.OrderAsync(_order);
        Assert.Multiple(() =>
        {
            Assert.That(part.QuantidadeEstoque, Is.EqualTo(10)); Assert.That(part.Ativo, Is.False);
            Assert.That(reservations.Count, Is.EqualTo(1)); Assert.That(reservations.Single().Devolvida, Is.True);
            Assert.That(order.GetProperty("status").GetInt32(), Is.EqualTo(7)); Assert.That(order.GetProperty("acaoPendente").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }
    private async Task FinishDiagnosis()
    {
        Assert.That((await _host.Execution.RequestAsync(HttpMethod.Post, $"/ordens/{_order}/concluir-diagnostico")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await _host.WaitOsAsync(_order, 3);
    }
}
