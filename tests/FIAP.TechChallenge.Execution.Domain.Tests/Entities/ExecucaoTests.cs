using FIAP.TechChallenge.Execution.Domain.Entities;

namespace FIAP.TechChallenge.Execution.Domain.Tests.Entities;

public sealed class ExecucaoTests
{
    private static Execucao Create() => Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Motor com falha", DateTime.UtcNow).Value!;

    [TestCase(0)][TestCase(1)][TestCase(2)]
    public void Create_ShouldRejectMissingIdentifiers(int field)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }; ids[field] = Guid.Empty;
        Assert.That(Execucao.Create(ids[0], ids[1], ids[2], "Motor", DateTime.UtcNow).IsSuccess, Is.False);
    }
    [TestCase("")][TestCase("ab")][TestCase("   ")]
    public void Create_ShouldRejectInvalidDescription(string value) => Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), value, DateTime.UtcNow).IsSuccess, Is.False);
    [Test]
    public void Create_ShouldRejectLongDescriptionOrMissingDate()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('x', 1001), DateTime.UtcNow).IsSuccess, Is.False);
            Assert.That(Execucao.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Motor", default).IsSuccess, Is.False);
        });
    }
    [Test]
    public void Flow_ShouldPreserveSnapshotsAndRequireCompletedServices()
    {
        var value = Create(); var now = DateTime.UtcNow;
        Assert.That(value.IniciarDiagnostico(now).IsSuccess, Is.True);
        var service = Servico.Create("Troca do filtro", 17.25m).Value!;
        var part = PecaInsumo.Create("Filtro de oleo", "FO", null, 12.75m, 10).Value!;
        var item = value.AdicionarServico(service, 2).Value!;
        value.AdicionarPeca(part, 3);
        service.UpdateValorUnitario(99m); part.UpdatePrecoUnitario(99m);
        value.ConcluirDiagnostico(now.AddMinutes(1));
        Assert.Multiple(() =>
        {
            Assert.That(value.ValorTotalServicos, Is.EqualTo(34.50m)); Assert.That(value.ValorTotalPecasInsumos, Is.EqualTo(38.25m));
            Assert.That(value.ValorTotal, Is.EqualTo(72.75m)); Assert.That(value.ConcluirServico(item.Id, 1).IsSuccess, Is.False);
        });
        Assert.That(value.Autorizar(now.AddMinutes(2)).IsSuccess, Is.True);
        Assert.That(value.Finalizar(now).IsSuccess, Is.False);
        Assert.That(value.ConcluirServico(item.Id, 25).IsSuccess, Is.True);
        Assert.That(value.Finalizar(now.AddMinutes(3)).IsSuccess, Is.True);
        var restored = Execucao.Rehydrate(value.Snapshot());
        Assert.Multiple(() =>
        {
            Assert.That(restored.Estado, Is.EqualTo(EstadoExecucao.Finalizada)); Assert.That(restored.DataFinalizacao, Is.Not.Null);
            Assert.That(restored.Servicos.Single().Concluido, Is.True); Assert.That(restored.StreamSequence, Is.EqualTo(4));
        });
    }
    [TestCase(EstadoExecucao.Recebida)][TestCase(EstadoExecucao.EmDiagnostico)][TestCase(EstadoExecucao.EmExecucao)][TestCase(EstadoExecucao.Finalizada)][TestCase(EstadoExecucao.Cancelada)]
    public void Cancel_ShouldRejectOutsideAwaitingAuthorization(EstadoExecucao state)
    {
        var value = Create(); value = Execucao.Rehydrate(value.Snapshot() with { Estado = state });
        Assert.That(value.Cancelar().IsSuccess, Is.False);
    }
    [Test]
    public void Cancel_ShouldFreezeItemsAndPreserveTheirIdentity()
    {
        var value = Create(); value.IniciarDiagnostico(DateTime.UtcNow);
        value.ConcluirDiagnostico(DateTime.UtcNow); var original = value.Snapshot();
        Assert.That(value.Cancelar().IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(value.Estado, Is.EqualTo(EstadoExecucao.Cancelada)); Assert.That(value.OrdemServicoId, Is.EqualTo(original.OrdemServicoId));
            Assert.That(value.IniciarDiagnostico(DateTime.UtcNow).IsSuccess, Is.False); Assert.That(value.Autorizar(DateTime.UtcNow).IsSuccess, Is.False);
        });
    }
    [TestCase(EstadoExecucao.Recebida)][TestCase(EstadoExecucao.AguardandoAprovacao)][TestCase(EstadoExecucao.EmExecucao)][TestCase(EstadoExecucao.Finalizada)]
    public void Items_ShouldRejectOutsideDiagnosis(EstadoExecucao state)
    {
        var value = Execucao.Rehydrate(Create().Snapshot() with { Estado = state });
        Assert.Multiple(() =>
        {
            Assert.That(value.AdicionarServico(Servico.Create("Servico", 0).Value!, 1).IsSuccess, Is.False);
            Assert.That(value.AdicionarPeca(PecaInsumo.Create("Filtro", "FI", null, 0, 1).Value!, 1).IsSuccess, Is.False);
        });
    }
    [Test]
    public void InvalidItems_ShouldNotChangeTheAggregate()
    {
        var value = Create(); value.IniciarDiagnostico(DateTime.UtcNow); var version = value.Version;
        Assert.Multiple(() =>
        {
            Assert.That(value.AdicionarServico(Servico.Create("Servico", 0).Value!, 0).IsSuccess, Is.False);
            Assert.That(value.AdicionarPeca(PecaInsumo.Create("Filtro", "FI", null, 0, 1).Value!, 0).IsSuccess, Is.False);
            Assert.That(value.Version, Is.EqualTo(version)); Assert.That(value.Servicos, Is.Empty); Assert.That(value.PecasInsumos, Is.Empty);
        });
    }
    [Test]
    public void InputSequence_ShouldRejectGapsAndPersistRejectedRequests()
    {
        var value = Create(); Assert.Throws<InvalidOperationException>(() => value.AcceptInput(3));
        value.AcceptInput(2); value.RejectInput();
        Assert.Multiple(() => { Assert.That(value.InputSequence, Is.EqualTo(2)); Assert.That(value.StreamSequence, Is.EqualTo(1)); Assert.That(value.Version, Is.EqualTo(1)); });
    }
    [Test]
    public void Lifecycle_ShouldRejectRepeatedTransitionsAndInvalidServiceCompletion()
    {
        var value = Create();
        Assert.Multiple(() => { Assert.That(value.ConcluirDiagnostico(DateTime.UtcNow).IsSuccess, Is.False); Assert.That(value.Finalizar(DateTime.UtcNow).IsSuccess, Is.False); });
        value.IniciarDiagnostico(DateTime.UtcNow); Assert.That(value.IniciarDiagnostico(DateTime.UtcNow).IsSuccess, Is.False);
        var item = value.AdicionarServico(Servico.Create("Servico", 0).Value!, 1).Value!;
        value.ConcluirDiagnostico(DateTime.UtcNow); value.Autorizar(DateTime.UtcNow);
        Assert.Multiple(() =>
        {
            Assert.That(value.ConcluirServico(Guid.NewGuid(), 1).IsSuccess, Is.False);
            Assert.That(value.ConcluirServico(item.Id, 0).IsSuccess, Is.False);
        });
        value.ConcluirServico(item.Id, 1);
        Assert.That(value.ConcluirServico(item.Id, 1).IsSuccess, Is.False);
    }
    [Test]
    public void Rehydrate_ShouldRejectCorruptSnapshots()
    {
        var snapshot = Create().Snapshot();
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Execucao.Rehydrate(snapshot with { Id = Guid.Empty }));
            Assert.Throws<InvalidOperationException>(() => Execucao.Rehydrate(snapshot with { Estado = (EstadoExecucao)99 }));
            Assert.Throws<InvalidOperationException>(() => Execucao.Rehydrate(snapshot with { Version = -1 }));
            Assert.Throws<InvalidOperationException>(() => Execucao.Rehydrate(snapshot with { DescricaoProblema = "" }));
        });
    }
}
