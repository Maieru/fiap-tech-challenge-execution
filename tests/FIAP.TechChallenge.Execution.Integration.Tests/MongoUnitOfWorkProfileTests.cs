using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Integration.Tests;

public sealed class MongoUnitOfWorkProfileTests
{
    [TestCase(null)]
    [TestCase(false)]
    [TestCase(true)]
    public async Task TransactionCommitsWithCausalConsistencyDisabledOnlyForDocumentDb(bool? documentDb)
    {
        var client = new MongoClient(TestEnvironment.Mongo.GetConnectionString());
        var name = "session_profile_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(name);
        IConfiguration? configuration = documentDb.HasValue
            ? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:UseDocumentDb"] = documentDb.Value.ToString()
            }).Build() : null;
        try
        {
            await database.CreateCollectionAsync("markers");
            var markers = database.GetCollection<BsonDocument>("markers");
            await using (var work = new MongoUnitOfWork(client, NullLogger<MongoUnitOfWork>.Instance, configuration))
            {
                await work.BeginAsync();
                var causalConsistency = work.Session!.Options.CausalConsistency;
                await markers.InsertOneAsync(work.Session, new BsonDocument("_id", "committed"));
                await work.CommitAsync();
                var committed = await markers.Find(new BsonDocument("_id", "committed")).AnyAsync();

                Assert.Multiple(() =>
                {
                    if (documentDb == true) Assert.That(causalConsistency, Is.False);
                    else Assert.That(causalConsistency, Is.Not.False);
                    Assert.That(committed, Is.True);
                });
            }
        }
        finally { await client.DropDatabaseAsync(name); }
    }
}
