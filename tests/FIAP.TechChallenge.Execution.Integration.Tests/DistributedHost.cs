using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.PostgreSql;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class DistributedHost : IAsyncDisposable
{
    public ExecutionHost Execution { get; } = new();
    private PostgreSqlContainer _postgres = null!;
    private IContainer _os = null!;
    public HttpClient Os { get; private set; } = null!;
    public const string OsRoot = "/api/os/v1";
    public async Task StartAsync()
    {
        await Execution.StartAsync(useImage: true);
        var password = Guid.NewGuid().ToString("N");
        _postgres = new PostgreSqlBuilder("postgres:18.3").WithDatabase("fiap_os").WithPassword(password)
            .WithNetwork(Execution.Network).WithNetworkAliases("os-db").Build();
        await _postgres.StartAsync();
        var image = Environment.GetEnvironmentVariable("OS_TEST_IMAGE") ?? throw new InvalidOperationException("OS_TEST_IMAGE deve apontar para a imagem do SHA compativel.");
        _os = new ContainerBuilder(image).WithNetwork(Execution.Network).WithPortBinding(8080, true)
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Testing").WithEnvironment("ASPNETCORE_HTTP_PORTS", "8080")
            .WithEnvironment("ConnectionStrings__DefaultConnection", $"Host=os-db;Database=fiap_os;Username=postgres;Password={password};GSS Encryption Mode=Disable")
            .WithEnvironment("Database__ApplyMigrations", "true")
            .WithEnvironment("Messaging__Enabled", "true").WithEnvironment("Messaging__BootstrapServers", "kafka:19092")
            .WithEnvironment("ManualExecutionAuthorizationEnabled", "true")
            .WithEnvironment("Jwt__Issuer", "test-issuer").WithEnvironment("Jwt__Audience", "test-audience").WithEnvironment("Jwt__SigningKey", ExecutionHost.Key)
            .WithEnvironment("Logging__LogLevel__Default", "Warning")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8080).ForPath(OsRoot + "/health/ready")))
            .Build();
        await _os.StartAsync();
        Os = new HttpClient { BaseAddress = new Uri($"http://{_os.Hostname}:{_os.GetMappedPublicPort(8080)}") };
        Os.DefaultRequestHeaders.Authorization = new("Bearer", ExecutionHost.Token());
        if (Environment.GetEnvironmentVariable("E2E_ARTIFACTS_DIRECTORY") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output, "os-openapi.json"), await Os.GetStringAsync("/openapi/v1.json"));
            await File.WriteAllTextAsync(Path.Combine(output, "execution-openapi.json"), await Execution.Client.GetStringAsync("/openapi/v1.json"));
        }
    }
    public async Task<JsonElement> CreateAsync(string path, object body)
    {
        var response = await Os.PostAsJsonAsync(OsRoot + path, body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Multiple(() => { Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), text); Assert.That(response.Headers.Location, Is.Not.Null); });
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    public async Task<JsonElement> OrderAsync(Guid id) => JsonDocument.Parse(await Os.GetStringAsync(OsRoot + "/ordens/" + id)).RootElement.Clone();
    public Task WaitOsAsync(Guid id, int status) => ExecutionHost.WaitAsync(async () => (await OrderAsync(id)).GetProperty("status").GetInt32() == status, 45);
    public async ValueTask DisposeAsync()
    {
        Os?.Dispose(); if (_os is not null) await _os.DisposeAsync(); if (_postgres is not null) await _postgres.DisposeAsync(); await Execution.DisposeAsync();
    }
}
