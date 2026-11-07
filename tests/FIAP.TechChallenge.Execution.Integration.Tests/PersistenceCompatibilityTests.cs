using FIAP.TechChallenge.Execution.Infrastructure;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class PersistenceCompatibilityTests
{
    private static ServiceProvider Services(string databaseName)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mongo:ConnectionString"] = TestEnvironment.Mongo.GetConnectionString(),
            ["Mongo:DatabaseName"] = databaseName
        }).Build();
        return new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration)
            .AddInfrastructure(configuration).BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Test]
    public async Task CompatibilityGateExercisesTransactionsAndRemovesOnlyItsIsolatedDatabase()
    {
        var name = "fiap_compat_" + Guid.NewGuid().ToString("N");
        await using var services = Services(name);
        await PersistenceCompatibility.VerifyAsync(services);
        var client = services.GetRequiredService<IMongoClient>();
        var databases = await (await client.ListDatabaseNamesAsync()).ToListAsync();
        Assert.That(databases, Does.Not.Contain(name));
    }

    [Test]
    public async Task CompatibilityGateRejectsAnUnrelatedDatabaseWithoutDeletingItsData()
    {
        var name = "protected_test_" + Guid.NewGuid().ToString("N");
        await using var services = Services(name);
        var database = services.GetRequiredService<IMongoDatabase>();
        var collection = database.GetCollection<BsonDocument>("marker");
        await collection.InsertOneAsync(new BsonDocument("marker", "preserve"));
        try
        {
            var rejected = false;
            try { await PersistenceCompatibility.VerifyAsync(services); }
            catch (InvalidOperationException) { rejected = true; }
            var count = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(rejected, Is.True);
                Assert.That(count, Is.EqualTo(1));
            });
        }
        finally { await services.GetRequiredService<IMongoClient>().DropDatabaseAsync(name); }
    }
}
