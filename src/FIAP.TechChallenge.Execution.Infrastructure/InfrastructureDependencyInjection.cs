using FIAP.TechChallenge.Execution.Domain.Interfaces;
using FIAP.TechChallenge.Execution.Infrastructure.Messaging;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        services.AddSingleton<IMongoClient>(_ => new MongoClient(configuration.GetConnectionString("MongoDB")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:MongoDB com replicaSet.")));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(configuration["MongoDB:Database"] ?? "fiap_execution"));
        services.AddSingleton<MongoBootstrap>();
        services.AddScoped<MongoSession>();
        services.AddScoped<ITransactionRunner>(provider => provider.GetRequiredService<MongoSession>());
        services.AddScoped<IServicoRepository, MongoServicoRepository>();
        services.AddScoped<IPecaInsumoRepository, MongoPecaRepository>();
        services.AddScoped<MongoExecutionRepository>();
        services.AddScoped<IExecutionRepository>(provider => provider.GetRequiredService<MongoExecutionRepository>());
        services.AddScoped<IServicoDaOrdemDeServicoRepository>(provider => provider.GetRequiredService<MongoExecutionRepository>());
        services.AddScoped<ICommandStore, MongoCommandStore>();
        services.AddScoped<IReservations, MongoReservations>();
        services.AddScoped<IntegrationContext>();
        services.AddScoped<MongoMessageStore>();
        services.AddScoped<IInbox>(provider => provider.GetRequiredService<MongoMessageStore>());
        services.AddScoped<IIntegrationEvents>(provider => provider.GetRequiredService<MongoMessageStore>());
        services.AddScoped<IOutboxStore>(provider => provider.GetRequiredService<MongoMessageStore>());
        services.AddScoped<IIntegrationEventHandler>(provider => provider.GetRequiredService<MongoMessageStore>());
        services.AddSingleton(provider =>
        {
            var finalConfiguration = provider.GetRequiredService<IConfiguration>();
            var options = finalConfiguration.GetSection("Messaging").Get<KafkaOptions>() ?? new();
            options.RetryDelaysSeconds = finalConfiguration.GetSection("Messaging:RetryDelaysSeconds").Get<int[]>() ?? [1, 2, 4, 8, 16];
            options.Enabled = finalConfiguration.GetValue("Messaging:Enabled", true);
            options.InputTopic = "fiap.os.events.v1"; options.OutputTopic = "fiap.execution.events.v1"; options.GroupId = "fiap-execution-v1";
            return options;
        });
        services.AddHostedService<KafkaWorker>(); services.AddHealthChecks().AddCheck<KafkaHealthCheck>("kafka");
        services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");
        return services;
    }
}
public sealed class MongoHealthCheck(IMongoDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try { await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken); return HealthCheckResult.Healthy(); }
        catch (MongoException) { return HealthCheckResult.Unhealthy(); }
    }
}
