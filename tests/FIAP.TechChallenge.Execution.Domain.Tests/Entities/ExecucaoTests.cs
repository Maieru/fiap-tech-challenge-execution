using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Tests.Entities;

public sealed class ExecucaoTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-01T12:00:00Z");
    private static Execucao Create() => Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Motor falhando", Now).Value!;
    private static Servico Service() => Servico.Create("Revisão", 100.25m).Value!;
    private static PecaInsumo Part() => PecaInsumo.Create("Filtro", "fl-01", null, 20.15m, 10).Value!;

    [Test]
    public void Stock_ShouldRejectIntegerOverflowWithoutChangingBalance()
    {
        var part = PecaInsumo.Create("Filtro", "FL-MAX", null, 1, int.MaxValue).Value!;
        var result = part.AddEstoque(1);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(part.QuantidadeEstoque, Is.EqualTo(int.MaxValue));
        });
    }

    [Test]
    public void Create_ShouldRejectInvalidIdentityDescriptionAndDate()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Execucao.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "Falha", Now).IsSuccess, Is.False);
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Falha", Now).IsSuccess, Is.False);
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Falha", Now).IsSuccess, Is.False);
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), " ", Now).IsSuccess, Is.False);
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('x', 1001), Now).IsSuccess, Is.False);
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Falha", default).IsSuccess, Is.False);
        });
    }

    [Test]
    public void Workflow_ShouldPreserveSnapshotsAndRequireCompletedServices()
    {
        var order = Create();
        var service = Service();
        var part = Part();
        Assert.That(order.IniciarDiagnostico(Now.AddMinutes(1)).IsSuccess, Is.True);
        var item = order.AdicionarServico(service, 2).Value!;
        _ = order.AdicionarPeca(part, 3);
        _ = service.UpdateDescricao("Alterado");
        _ = service.UpdateValorUnitario(999);
        _ = part.UpdatePrecoUnitario(999);
        _ = order.ConcluirDiagnostico(Now.AddMinutes(2));
        _ = order.IniciarExecucao(Now.AddMinutes(3));
        Assert.Multiple(() =>
        {
            Assert.That(order.Finalizar(Now.AddMinutes(4)).IsSuccess, Is.False);
            Assert.That(order.ConcluirServico(item.Id, 0).IsSuccess, Is.False);
            Assert.That(order.ConcluirServico(Guid.NewGuid(), 10).IsSuccess, Is.False);
            Assert.That(order.ValorTotal, Is.EqualTo(260.95m));
            Assert.That(order.Servicos.Single().Descricao, Is.EqualTo("Revisão"));
        });
        _ = order.ConcluirServico(item.Id, 15);
        Assert.That(order.ConcluirServico(item.Id, 15).IsSuccess, Is.False);
        _ = order.Finalizar(Now.AddMinutes(5));
        var recovered = Execucao.Rehydrate(order.Snapshot()).Value!;
        Assert.Multiple(() =>
        {
            Assert.That(recovered.Status, Is.EqualTo(StatusExecucao.Finalizada));
            Assert.That(recovered.ValorTotal, Is.EqualTo(260.95m));
            Assert.That(recovered.Servicos.Single().TempoGastoMinutos, Is.EqualTo(15));
            Assert.That(recovered.Snapshot().FimExecucaoUtc, Is.EqualTo(Now.AddMinutes(5)));
        });
    }

    [Test]
    public void Workflow_ShouldRejectOutOfOrderTransitionsAndEdits()
    {
        var order = Create();
        Assert.Multiple(() =>
        {
            Assert.That(order.ConcluirDiagnostico(Now).IsSuccess, Is.False);
            Assert.That(order.IniciarExecucao(Now).IsSuccess, Is.False);
            Assert.That(order.Cancelar(Now).IsSuccess, Is.False);
            Assert.That(order.Finalizar(Now).IsSuccess, Is.False);
            Assert.That(order.ConcluirServico(Guid.NewGuid(), 10).IsSuccess, Is.False);
            Assert.That(order.AdicionarServico(Service(), 1).IsSuccess, Is.False);
            Assert.That(order.AdicionarPeca(Part(), 1).IsSuccess, Is.False);
        });
        _ = order.IniciarDiagnostico(Now);
        Assert.Multiple(() =>
        {
            Assert.That(order.IniciarDiagnostico(Now).IsSuccess, Is.False);
            Assert.That(order.AdicionarServico(Service(), 0).IsSuccess, Is.False);
            Assert.That(order.AdicionarPeca(Part(), -1).IsSuccess, Is.False);
        });
        var inactive = Part();
        _ = inactive.Inactivate();
        Assert.That(order.AdicionarPeca(inactive, 1).IsSuccess, Is.False);
        _ = order.ConcluirDiagnostico(Now);
        Assert.Multiple(() =>
        {
            Assert.That(order.AdicionarServico(Service(), 1).IsSuccess, Is.False);
            Assert.That(order.AdicionarPeca(Part(), 1).IsSuccess, Is.False);
        });
        _ = order.Cancelar(Now);
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(StatusExecucao.Cancelada));
            Assert.That(order.Cancelar(Now).IsSuccess, Is.False);
            Assert.That(order.IniciarExecucao(Now).IsSuccess, Is.False);
        });
    }

    [Test]
    public void Rehydrate_ShouldRejectInconsistentDatesAndForeignItems()
    {
        var order = Create();
        _ = order.IniciarDiagnostico(Now);
        var item = order.AdicionarServico(Service(), 1).Value!;
        var snapshot = order.Snapshot();
        Assert.Multiple(() =>
        {
            Assert.That(Execucao.Rehydrate(snapshot with { InicioDiagnosticoUtc = null }).IsSuccess, Is.False);
            Assert.That(Execucao.Rehydrate(snapshot with { Status = (StatusExecucao)99 }).IsSuccess, Is.False);
            Assert.That(Execucao.Rehydrate(snapshot with { Servicos = [snapshot.Servicos[0] with { OrdemServicoId = Guid.NewGuid() }] }).IsSuccess, Is.False);
            Assert.That(Execucao.Rehydrate(snapshot with { Servicos = [snapshot.Servicos[0], snapshot.Servicos[0]] }).IsSuccess, Is.False);
        });
    }

    [Test]
    public void Finalizar_ShouldAllowOrderWithoutLaborServices_AsInLegacy()
    {
        var order = Create();
        _ = order.IniciarDiagnostico(Now);
        _ = order.AdicionarPeca(Part(), 1);
        _ = order.ConcluirDiagnostico(Now);
        _ = order.IniciarExecucao(Now);
        Assert.That(order.Finalizar(Now).IsSuccess, Is.True);
    }
}
