using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure.Persistence;

public sealed class MongoStore(IMongoDatabase database, MongoUnitOfWork unitOfWork)
{
    public IMongoCollection<T> Collection<T>(string name) => database.GetCollection<T>(name);
    public IFindFluent<T, T> Find<T>(IMongoCollection<T> collection, FilterDefinition<T> filter) =>
        unitOfWork.Session is { } session ? collection.Find(session, filter) : collection.Find(filter);
    public Task InsertAsync<T>(IMongoCollection<T> collection, T document, CancellationToken token) =>
        unitOfWork.Session is { } session ? collection.InsertOneAsync(session, document, cancellationToken: token)
            : collection.InsertOneAsync(document, cancellationToken: token);
    public Task<ReplaceOneResult> ReplaceAsync<T>(IMongoCollection<T> collection, FilterDefinition<T> filter, T document, CancellationToken token) =>
        unitOfWork.Session is { } session ? collection.ReplaceOneAsync(session, filter, document, cancellationToken: token)
            : collection.ReplaceOneAsync(filter, document, cancellationToken: token);
}
