using Confluent.Kafka;
using FIAP.TechChallenge.Execution.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FIAP.TechChallenge.Execution.API;

public sealed class ExecutionReadiness(IMongoDatabase database, IConfiguration configuration, ConsumerState consumerState) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!consumerState.Running)
                return HealthCheckResult.Unhealthy("Consumidor não está ativo.");

            _ = await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = configuration["Kafka:BootstrapServers"] }).Build();
            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(3));
            return metadata.Brokers.Count > 0 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { return HealthCheckResult.Unhealthy(); }
    }
}
