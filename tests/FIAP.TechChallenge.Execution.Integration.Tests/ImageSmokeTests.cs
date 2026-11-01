using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

[Category("ContainerImage")]
public sealed class ImageSmokeTests
{
    [Test]
    public async Task Image_ShouldAuthenticateServeOpenApiAndPreserveCatalogAfterRestart()
    {
        var image = Environment.GetEnvironmentVariable("EXECUTION_TEST_IMAGE");
        if (string.IsNullOrWhiteSpace(image)) Assert.Ignore("Set EXECUTION_TEST_IMAGE after building the image.");
        var databaseName = $"image_{Guid.NewGuid():N}";
        var topic = $"image-input-{Guid.NewGuid():N}";
        using var admin = TestEnvironment.Admin();
        await admin.CreateTopicsAsync([new Confluent.Kafka.Admin.TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
        var mongo = new MongoUrlBuilder(TestEnvironment.Mongo.GetConnectionString())
        { Server = new MongoServerAddress("mongo", 27017), DirectConnection = true };
        await using var container = new ContainerBuilder(image!)
            .WithNetwork(TestEnvironment.Network)
            .WithPortBinding(8080, true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing")
            .WithEnvironment("Mongo__ConnectionString", mongo.ToString())
            .WithEnvironment("Mongo__DatabaseName", databaseName)
            .WithEnvironment("Kafka__BootstrapServers", "kafka:19092")
            .WithEnvironment("Kafka__InputTopic", topic)
            .WithEnvironment("Kafka__GroupId", $"image-{Guid.NewGuid():N}")
            .WithEnvironment("Jwt__Issuer", "execution-tests")
            .WithEnvironment("Jwt__Audience", "execution-tests")
            .WithEnvironment("Jwt__SigningKey", ExecutionFactory.SigningKey)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request =>
                request.ForPort(8080).ForPath("/api/execution/v1/health/ready")))
            .Build();
        try
        {
            await container.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(8080)}") };
            using var unauthorized = await client.GetAsync("/api/execution/v1/servicos");
            Assert.That(unauthorized.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ExecutionFactory.Token());
            using var created = await client.PostAsJsonAsync("/api/execution/v1/servicos", new { descricao = "Revisão", valorUnitario = 100.25m });
            var payload = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), payload.ToString());
            var id = payload.GetProperty("id").GetGuid();
            using var openapi = await client.GetAsync("/openapi/v1.json");
            var specification = await openapi.Content.ReadAsStringAsync();
            Assert.Multiple(() =>
            {
                Assert.That(openapi.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(specification, Does.Contain("/api/execution/v1/ordens/{id}/finalizar"));
            });
            var output = Environment.GetEnvironmentVariable("EXECUTION_OPENAPI_OUTPUT");
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                await File.WriteAllTextAsync(output, specification);
            }
            await container.StopAsync();
            await container.StartAsync();
            using var restartedClient = new HttpClient
            { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(8080)}") };
            restartedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ExecutionFactory.Token());
            using var recovered = await restartedClient.GetAsync($"/api/execution/v1/servicos/{id}");
            var snapshot = await recovered.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Multiple(() =>
            {
                Assert.That(recovered.StatusCode, Is.EqualTo(HttpStatusCode.OK));
                Assert.That(snapshot.GetProperty("valorUnitario").GetDecimal(), Is.EqualTo(100.25m));
            });
        }
        finally
        {
            await container.StopAsync();
            await new MongoClient(TestEnvironment.Mongo.GetConnectionString()).DropDatabaseAsync(databaseName);
            await admin.DeleteTopicsAsync([topic]);
        }
    }
}
