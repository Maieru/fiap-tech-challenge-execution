using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FIAP.TechChallenge.Execution.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Security.Tests;

public sealed class MongoConfigurationTests
{
    // No database operations occur in these configuration tests.
    private const string Connection = "mongodb://configuration.invalid:27017/?retryWrites=true&readPreference=secondary";

    private static ServiceProvider Services(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure(configuration).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [TestCase(false, false)]
    [TestCase(true, true)]
    [TestCase(false, true)]
    public async Task CustomCaEnablesTlsIndependentlyOfDocumentDbProfile(bool enabled, bool customCa)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Configuration Test CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificatePath = Path.Combine(Path.GetTempPath(), "mongo-config-ca-" + Guid.NewGuid().ToString("N") + ".pem");
        File.WriteAllText(certificatePath, certificate.ExportCertificatePem());
        try
        {
            await using var services = Services(new()
            {
                ["Mongo:ConnectionString"] = Connection,
                ["Mongo:DatabaseName"] = "configuration_test",
                ["Mongo:UseDocumentDb"] = enabled.ToString(),
                ["Mongo:CaCertificatePath"] = customCa ? certificatePath : null
            });
            var client = services.GetRequiredService<IMongoClient>();
            var database = services.GetRequiredService<IMongoDatabase>();

            Assert.Multiple(() =>
            {
                Assert.That(client.Settings.RetryWrites, Is.EqualTo(!enabled));
                Assert.That(client.Settings.UseTls, Is.EqualTo(customCa));
                Assert.That(client.Settings.AllowInsecureTls, Is.False);
                Assert.That(client.Settings.ReadPreference,
                    Is.EqualTo(enabled ? ReadPreference.Primary : ReadPreference.Secondary));
                Assert.That(client.Settings.ServerSelectionTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(client.Settings.ConnectTimeout, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(database.DatabaseNamespace.DatabaseName, Is.EqualTo("configuration_test"));
                if (customCa) Assert.That(client.Settings.SslSettings.ServerCertificateValidationCallback, Is.Not.Null);
            });
        }
        finally { File.Delete(certificatePath); }
    }

    [Test]
    public async Task DocumentDbProfileRequiresCertificatePath()
    {
        await using var services = Services(new()
        {
            ["Mongo:ConnectionString"] = Connection,
            ["Mongo:UseDocumentDb"] = "true"
        });

        Assert.That(() => services.GetRequiredService<IMongoClient>(),
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Configure Mongo:CaCertificatePath."));
    }

    [Test]
    public async Task ClientRequiresConnectionString()
    {
        await using var services = Services(new());

        Assert.That(() => services.GetRequiredService<IMongoClient>(),
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Configure Mongo:ConnectionString."));
    }

    [Test]
    public async Task DatabaseRequiresDatabaseName()
    {
        await using var services = Services(new() { ["Mongo:ConnectionString"] = Connection });

        Assert.That(() => services.GetRequiredService<IMongoDatabase>(),
            Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("Configure Mongo:DatabaseName."));
    }
}
