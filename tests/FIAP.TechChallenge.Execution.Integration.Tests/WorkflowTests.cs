using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FIAP.TechChallenge.Execution.Domain.Entities;
using FIAP.TechChallenge.OS.Application.Messaging;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class WorkflowTests : IntegrationTest
{
    [Test]
    public async Task Workflow_ShouldConsumeOrderPreserveSnapshotsAndCompleteExecution()
    {
        var service = await ServiceAsync();
        var part = await PartAsync();
        var id = await ReceiveOrderAsync();
        await PutAsync($"ordens/{id}/iniciar-diagnostico");
        using var laborResponse = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addservico", new { ordemServicoId = Guid.NewGuid(), servicoId = service, quantidade = 2 });
        _ = await ReadAsync(laborResponse, HttpStatusCode.Created);
        using var partResponse = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addpecainsumo", new { pecaInsumoId = part, quantidade = 3 });
        _ = await ReadAsync(partResponse, HttpStatusCode.Created);
        await PutAsync($"servicos/{service}", new { descricao = "Catálogo alterado", valorUnitario = 999 });
        using var deleted = await Client.DeleteAsync($"{Prefix}/pecas-insumos/{part}");
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        await PutAsync($"ordens/{id}/solicitar-aprovacao");
        var diagnosis = ReadEvent(id, Application.Messaging.DiagnosticoConcluido.Type);
        Assert.That(diagnosis.Payload.GetProperty("valorTotal").GetDecimal(), Is.EqualTo(260.95m));
        await SendAsync(id, ExecucaoAutorizada.Type, new ExecucaoAutorizada(id));
        var executing = await WaitOrderAsync(id, StatusExecucao.EmExecucao);
        await PutAsync($"ordens/{id}/finalizar", expected: HttpStatusCode.BadRequest);
        var item = executing.Execucao.Servicos.Single();
        await PutAsync($"ordens/{id}/servicos/{item.Id}/concluir", new { tempoGastoMinutos = 0 }, HttpStatusCode.BadRequest);
        await PutAsync($"ordens/{id}/servicos/{item.Id}/concluir", new { tempoGastoMinutos = 15 });
        await PutAsync($"ordens/{id}/finalizar");
        var finished = await WaitOrderAsync(id, StatusExecucao.Finalizada);
        using var average = await Client.GetAsync($"{Prefix}/servicos/{service}/tempo-medio");
        var mean = await ReadAsync(average);
        Assert.Multiple(() =>
        {
            Assert.That(finished.ValorTotal, Is.EqualTo(260.95m));
            Assert.That(finished.Execucao.Servicos.Single().Descricao, Is.EqualTo("Revisão"));
            Assert.That(finished.Execucao.PecasInsumos.Single().PrecoUnitario, Is.EqualTo(20.15m));
            Assert.That(finished.Execucao.FimExecucaoUtc, Is.Not.Null);
            Assert.That(mean.GetProperty("tempoMedioMinutos").GetDecimal(), Is.EqualTo(15m));
        });
        using var orders = await Client.GetAsync($"{Prefix}/ordens?status=Finalizada");
        var list = await ReadAsync(orders);
        Assert.That(list.GetProperty("totalItems").GetInt64(), Is.EqualTo(1));
    }

    [Test]
    public async Task Cancellation_ShouldRestoreStockOnce_WhenMessagesAreRedelivered()
    {
        var part = await PartAsync();
        var id = await ReceiveOrderAsync();
        await PutAsync($"ordens/{id}/iniciar-diagnostico");
        using var added = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addpecainsumo", new { pecaInsumoId = part, quantidade = 3 });
        _ = await ReadAsync(added, HttpStatusCode.Created);
        Assert.That(await StockAsync(part), Is.EqualTo(7));
        await PutAsync($"ordens/{id}/solicitar-aprovacao");
        var messageId = Guid.NewGuid();
        await SendAsync(id, CancelamentoSolicitado.Type, new CancelamentoSolicitado(id), messageId);
        await WaitOrderAsync(id, StatusExecucao.Cancelada);
        await SendAsync(id, CancelamentoSolicitado.Type, new CancelamentoSolicitado(id), messageId);
        await SendAsync(id, CancelamentoSolicitado.Type, new CancelamentoSolicitado(id));
        // A subsequent order is a barrier: Kafka processes the same input partition sequentially.
        await ReceiveOrderAsync();
        Assert.That(await StockAsync(part), Is.EqualTo(10));
    }

    [Test]
    public async Task Workflow_ShouldRejectInsufficientStockAndOutOfOrderActionsWithoutPartialWrites()
    {
        var part = await PartAsync(1);
        var id = await ReceiveOrderAsync();
        await PutAsync($"ordens/{id}/solicitar-aprovacao", expected: HttpStatusCode.BadRequest);
        await PutAsync($"ordens/{id}/iniciar-diagnostico");
        using var insufficient = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addpecainsumo", new { pecaInsumoId = part, quantidade = 2 });
        _ = await ReadAsync(insufficient, HttpStatusCode.BadRequest);
        using var missing = await Client.PostAsJsonAsync($"{Prefix}/ordens/{id}/addservico", new { servicoId = Guid.NewGuid(), quantidade = 1 });
        _ = await ReadAsync(missing, HttpStatusCode.NotFound);
        var order = await GetOrderAsync(id);
        Assert.Multiple(() =>
        {
            Assert.That(order.Execucao.PecasInsumos, Is.Empty);
            Assert.That(order.Execucao.Servicos, Is.Empty);
        });
        Assert.That(await StockAsync(part), Is.EqualTo(1));
        using var invalidPage = await Client.GetAsync($"{Prefix}/ordens?pageSize=101");
        _ = await ReadAsync(invalidPage, HttpStatusCode.BadRequest);
        await PutAsync($"ordens/{Guid.NewGuid()}/iniciar-diagnostico", expected: HttpStatusCode.NotFound);
    }
}
