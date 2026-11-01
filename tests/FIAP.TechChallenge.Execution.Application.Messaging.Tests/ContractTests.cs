using System.Text.Json;
using System.Text.Json.Nodes;

namespace FIAP.TechChallenge.Execution.Application.Messaging.Tests;

public sealed class ContractTests
{
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IEnumerable<TestCaseData> Transitions()
    {
        yield return new TestCaseData(new DiagnosticoIniciado(OrderId));
        yield return new TestCaseData(new ExecucaoIniciada(OrderId));
        yield return new TestCaseData(new ExecucaoConcluida(OrderId));
        yield return new TestCaseData(new ExecucaoCancelada(OrderId));
    }

    [TestCaseSource(nameof(Transitions))]
    public void Transitions_ShouldPreserveVersionOneJson(object message)
    {
        const string expected = """{"ordemServicoId":"11111111-1111-1111-1111-111111111111"}""";
        var actual = JsonSerializer.Serialize(message, message.GetType(), Options);
        var recovered = JsonSerializer.Deserialize(expected, message.GetType(), Options);
        Assert.Multiple(() =>
        {
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)), Is.True);
            Assert.That(recovered, Is.EqualTo(message));
        });
    }

    [Test]
    public void Diagnosis_ShouldPreserveHistoricalItemsAndDecimalPrices()
    {
        const string json = """
            {
              "ordemServicoId": "11111111-1111-1111-1111-111111111111",
              "servicos": [{
                "itemId": "22222222-2222-2222-2222-222222222222",
                "servicoId": "33333333-3333-3333-3333-333333333333",
                "descricao": "Revisao", "valorUnitario": 100.25, "quantidade": 2, "valorTotal": 200.50
              }],
              "pecasInsumos": [{
                "itemId": "44444444-4444-4444-4444-444444444444",
                "pecaInsumoId": "55555555-5555-5555-5555-555555555555",
                "nome": "Filtro", "codigo": "FL-01", "descricao": null,
                "precoUnitario": 20.15, "quantidade": 3, "valorTotal": 60.45
              }],
              "valorTotal": 260.95
            }
            """;
        var message = JsonSerializer.Deserialize<DiagnosticoConcluido>(json, Options)!;
        var roundTrip = JsonSerializer.Serialize(message, Options);
        Assert.Multiple(() =>
        {
            Assert.That(message.OrdemServicoId, Is.EqualTo(OrderId));
            Assert.That(message.Servicos.Single().ValorUnitario, Is.EqualTo(100.25m));
            Assert.That(message.PecasInsumos.Single().PrecoUnitario, Is.EqualTo(20.15m));
            Assert.That(message.PecasInsumos.Single().Descricao, Is.Null);
            Assert.That(message.ValorTotal, Is.EqualTo(260.95m));
            Assert.That(JsonNode.DeepEquals(JsonNode.Parse(roundTrip), JsonNode.Parse(json)), Is.True);
        });
    }

    [Test]
    public void Routing_ShouldKeepPublishedIdentifiersStable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ExecutionMessaging.Topic, Is.EqualTo("oficina.execution.events.v1"));
            Assert.That(ExecutionMessaging.Version, Is.EqualTo(1));
            Assert.That(DiagnosticoIniciado.Type, Is.EqualTo("execution.diagnostico-iniciado"));
            Assert.That(DiagnosticoConcluido.Type, Is.EqualTo("execution.diagnostico-concluido"));
            Assert.That(ExecucaoIniciada.Type, Is.EqualTo("execution.execucao-iniciada"));
            Assert.That(ExecucaoConcluida.Type, Is.EqualTo("execution.execucao-concluida"));
            Assert.That(ExecucaoCancelada.Type, Is.EqualTo("execution.execucao-cancelada"));
            Assert.That(typeof(DiagnosticoConcluido).Assembly.GetReferencedAssemblies()
                .Any(x => x.Name!.StartsWith("FIAP.TechChallenge.") || x.Name.StartsWith("Confluent.")), Is.False);
        });
    }
}
