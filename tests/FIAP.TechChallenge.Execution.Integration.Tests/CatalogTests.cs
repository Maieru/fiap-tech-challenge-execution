using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class CatalogTests : IntegrationTest
{
    [Test]
    public async Task Services_ShouldSupportCrudSoftDeleteAndRouteIdentity()
    {
        var id = await ServiceAsync();
        using var get = await Client.GetAsync($"{Prefix}/servicos/{id}");
        var original = await ReadAsync(get);
        var updated = await PutAsync($"servicos/{id}", new { id = Guid.NewGuid(), descricao = "Revisão completa", valorUnitario = 150m });
        using var list = await Client.GetAsync($"{Prefix}/servicos?pageNumber=1&pageSize=10");
        var listed = await ReadAsync(list);
        using var average = await Client.GetAsync($"{Prefix}/servicos/{id}/tempo-medio");
        var times = await ReadAsync(average);
        Assert.Multiple(() =>
        {
            Assert.That(original.GetProperty("valorUnitario").GetDecimal(), Is.EqualTo(100.25m));
            Assert.That(updated.GetProperty("id").GetGuid(), Is.EqualTo(id));
            Assert.That(listed.GetProperty("totalItems").GetInt32(), Is.EqualTo(1));
            Assert.That(times.GetProperty("quantidadeExecucoes").GetInt32(), Is.Zero);
        });
        using var deleted = await Client.DeleteAsync($"{Prefix}/servicos/{id}");
        using var missing = await Client.GetAsync($"{Prefix}/servicos/{id}");
        var missingBody = await ReadAsync(missing, HttpStatusCode.NotFound);
        Assert.Multiple(() =>
        {
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(missingBody.GetProperty("errorCode").GetString(), Is.EqualTo("NotFound"));
        });
    }

    [Test]
    public async Task Parts_ShouldNormalizeCodeUpdateStockAndRetainInactiveRows()
    {
        var id = await PartAsync(10, " fl-01 ");
        var stock = await PutAsync($"pecas-insumos/{id}/entrada-estoque", new { quantidade = 5 });
        var updated = await PutAsync($"pecas-insumos/{id}", new { id = Guid.NewGuid(), nome = "Filtro novo", codigo = "FL-01", precoUnitario = 30m, ativo = true });
        using var byCode = await Client.GetAsync($"{Prefix}/pecas-insumos?codigo=fl-01");
        var found = await ReadAsync(byCode);
        using var list = await Client.GetAsync($"{Prefix}/pecas-insumos");
        var listed = await ReadAsync(list);
        Assert.Multiple(() =>
        {
            Assert.That(stock.GetProperty("quantidadeEstoque").GetInt32(), Is.EqualTo(15));
            Assert.That(updated.GetProperty("id").GetGuid(), Is.EqualTo(id));
            Assert.That(found.GetProperty("codigo").GetString(), Is.EqualTo("FL-01"));
            Assert.That(listed.GetProperty("totalItems").GetInt32(), Is.EqualTo(1));
        });
        using var duplicate = await Client.PostAsJsonAsync($"{Prefix}/pecas-insumos", new
        { nome = "Outro filtro", codigo = "fl-01", precoUnitario = 10, quantidadeEstoque = 1 });
        _ = await ReadAsync(duplicate, HttpStatusCode.Conflict);
        using var deleted = await Client.DeleteAsync($"{Prefix}/pecas-insumos/{id}");
        using var missing = await Client.GetAsync($"{Prefix}/pecas-insumos/{id}");
        _ = await ReadAsync(missing, HttpStatusCode.NotFound);
        var replacement = await PartAsync(1, "FL-01");
        Assert.Multiple(() =>
        {
            Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(replacement, Is.Not.EqualTo(id));
        });
    }

    [TestCase("servicos", "{\"descricao\":\" \",\"valorUnitario\":-1}")]
    [TestCase("pecas-insumos", "{\"nome\":\"x\",\"codigo\":\"x\",\"precoUnitario\":-1,\"quantidadeEstoque\":-1}")]
    public async Task Catalog_ShouldRejectInvalidInput(string resource, string json)
    {
        using var response = await Client.PostAsync($"{Prefix}/{resource}", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        var body = await ReadAsync(response, HttpStatusCode.BadRequest);
        Assert.That(body.GetProperty("errorCode").GetString(), Is.EqualTo("BadRequest"));
    }

    [TestCase("servicos")]
    [TestCase("pecas-insumos")]
    public async Task Catalog_ShouldReturnNotFoundAndRejectInvalidPagination(string resource)
    {
        using var missing = await Client.GetAsync($"{Prefix}/{resource}/{Guid.NewGuid()}");
        _ = await ReadAsync(missing, HttpStatusCode.NotFound);
        using var invalid = await Client.GetAsync($"{Prefix}/{resource}?pageNumber=0");
        _ = await ReadAsync(invalid, HttpStatusCode.BadRequest);
        using var update = await Client.PutAsJsonAsync($"{Prefix}/{resource}/{Guid.NewGuid()}", new
        { descricao = "Revisão", nome = "Filtro", codigo = "FL-404", valorUnitario = 10, precoUnitario = 10, ativo = true });
        _ = await ReadAsync(update, HttpStatusCode.NotFound);
        using var delete = await Client.DeleteAsync($"{Prefix}/{resource}/{Guid.NewGuid()}");
        _ = await ReadAsync(delete, HttpStatusCode.NotFound);
    }

    [TestCase("signature")]
    [TestCase("expiration")]
    [TestCase("issuer")]
    public async Task Authentication_ShouldRejectInvalidSignedTokens(string invalidField)
    {
        var token = invalidField switch
        {
            "signature" => ExecutionFactory.Token(signingKey: Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))),
            "expiration" => ExecutionFactory.Token(expired: true),
            _ => ExecutionFactory.Token(issuer: "another-issuer")
        };
        Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await Client.GetAsync($"{Prefix}/servicos");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("too-short")]
    public async Task Authentication_ShouldRejectMissingOrWeakEnvironmentKey(string? key)
    {
        var previous = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        try
        {
            Environment.SetEnvironmentVariable("Jwt__SigningKey", key);
            await using var invalid = new ExecutionFactory($"{DatabaseName}_invalid", InputTopic, $"{GroupId}-invalid");
            Assert.That(() => { using var client = invalid.CreateClient(); },
                Throws.TypeOf<InvalidOperationException>().With.Message.Contains("Jwt__SigningKey"));
        }
        finally { Environment.SetEnvironmentVariable("Jwt__SigningKey", previous); }
    }

    [Test]
    public async Task AuthenticationAndHealth_ShouldUseRealJwtAndPublicHealth()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        using var forbidden = await Client.GetAsync($"{Prefix}/servicos");
        using var live = await Client.GetAsync($"{Prefix}/health/live");
        using var ready = await Client.GetAsync($"{Prefix}/health/ready");
        var liveness = await ReadAsync(live);
        var readiness = await ReadAsync(ready);
        Assert.Multiple(() =>
        {
            Assert.That(forbidden.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(liveness.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
            Assert.That(readiness.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
        });
    }
}
