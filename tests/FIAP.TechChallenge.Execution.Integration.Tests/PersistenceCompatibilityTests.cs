using FIAP.TechChallenge.Execution.Infrastructure;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

[NonParallelizable]
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
    public async Task CliVerifiesPersistenceAndExitsWithoutJwtOrKafkaConfiguration()
    {
        var name = "fiap_compat_" + Guid.NewGuid().ToString("N");
        var client = new MongoClient(TestEnvironment.Mongo.GetConnectionString());
        try
        {
            await RunCliAsync(name).WaitAsync(TimeSpan.FromSeconds(30));

            var databases = await (await client.ListDatabaseNamesAsync()).ToListAsync();
            Assert.That(databases, Does.Not.Contain(name));
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Test]
    public async Task CliRejectsAnUnrelatedDatabaseAndPreservesItsData()
    {
        var name = "protected_cli_" + Guid.NewGuid().ToString("N");
        var client = new MongoClient(TestEnvironment.Mongo.GetConnectionString());
        var markers = client.GetDatabase(name).GetCollection<BsonDocument>("markers");
        await markers.InsertOneAsync(new BsonDocument("_id", "preserve"));
        try
        {
            InvalidOperationException? failure = null;
            try { await RunCliAsync(name).WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (InvalidOperationException exception) { failure = exception; }
            var count = await markers.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(failure, Is.Not.Null);
                Assert.That(count, Is.EqualTo(1));
            });
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Test]
    public async Task CancellationStillRemovesTheIsolatedCompatibilityDatabase()
    {
        var name = "fiap_compat_" + Guid.NewGuid().ToString("N");
        await using var services = Services(name);
        var client = services.GetRequiredService<IMongoClient>();
        await client.GetDatabase(name).GetCollection<BsonDocument>("markers")
            .InsertOneAsync(new BsonDocument("_id", "temporary"));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        try
        {
            OperationCanceledException? failure = null;
            try { await PersistenceCompatibility.VerifyAsync(services, cancellation.Token); }
            catch (OperationCanceledException exception) { failure = exception; }
            var databases = await (await client.ListDatabaseNamesAsync()).ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(failure, Is.Not.Null);
                Assert.That(databases, Does.Not.Contain(name));
            });
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    [Test]
    public async Task IncompatibleIndexFailsTheGateAndRemovesTheIsolatedDatabase()
    {
        var name = "fiap_compat_" + Guid.NewGuid().ToString("N");
        await using var services = Services(name);
        var client = services.GetRequiredService<IMongoClient>();
        var parts = client.GetDatabase(name).GetCollection<PecaDocument>("pecas_insumos");
        await parts.Indexes.CreateOneAsync(new CreateIndexModel<PecaDocument>(
            Builders<PecaDocument>.IndexKeys.Ascending(x => x.Nome),
            new CreateIndexOptions { Name = "codigo_ativo_unico" }));
        try
        {
            MongoCommandException? failure = null;
            try { await PersistenceCompatibility.VerifyAsync(services); }
            catch (MongoCommandException exception) { failure = exception; }
            var databases = await (await client.ListDatabaseNamesAsync()).ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(failure, Is.Not.Null);
                Assert.That(databases, Does.Not.Contain(name));
            });
        }
        finally { await client.DropDatabaseAsync(name); }
    }

    private static async Task RunCliAsync(string databaseName)
    {
        var previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        try
        {
            await API.Program.Main([
                "--Mongo:ConnectionString=" + TestEnvironment.Mongo.GetConnectionString(),
                "--Mongo:DatabaseName=" + databaseName,
                "--Kafka:BootstrapServers=",
                "--verify-persistence"
            ]);
        }
        finally { Environment.SetEnvironmentVariable("Jwt__SigningKey", previousSigningKey); }
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
