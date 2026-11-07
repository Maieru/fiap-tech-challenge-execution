using FIAP.TechChallenge.Execution.Application.Abstractions;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using FIAP.TechChallenge.Execution.Infrastructure.Messaging;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
using Confluent.Kafka;
using Maieru.FiapTechChallenge.Chassis;
using Maieru.FiapTechChallenge.Messaging.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        services.AddSingleton<IMongoClient>(provider =>
        {
            var connection = provider.GetRequiredService<IConfiguration>()["Mongo:ConnectionString"]
                ?? throw new InvalidOperationException("Configure Mongo:ConnectionString.");
            var settings = MongoClientSettings.FromConnectionString(connection);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            if (configuration.GetValue<bool>("Mongo:UseDocumentDb"))
            {
                settings.RetryWrites = false;
                settings.ReadPreference = ReadPreference.Primary;
                DocumentDbTls.Configure(settings, configuration["Mongo:CaCertificatePath"]
                    ?? throw new InvalidOperationException("Configure Mongo:CaCertificatePath."));
            }
            return new MongoClient(settings);
        });
        services.AddSingleton(provider => provider.GetRequiredService<IMongoClient>().GetDatabase(
            provider.GetRequiredService<IConfiguration>()["Mongo:DatabaseName"]
                ?? throw new InvalidOperationException("Configure Mongo:DatabaseName.")));
        services.AddScoped<MongoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<MongoUnitOfWork>());
        services.AddScoped<MongoStore>();
        services.AddScoped<ServicoRepository>();
        services.AddScoped<IServicoRepository>(provider => provider.GetRequiredService<ServicoRepository>());
        services.AddScoped<IServicoDaOrdemDeServicoRepository>(provider => provider.GetRequiredService<ServicoRepository>());
        services.AddScoped<IPecaInsumoRepository, PecaInsumoRepository>();
        services.AddScoped<IExecucaoRepository, ExecucaoRepository>();
        services.AddScoped<IProcessedMessageRepository, ProcessedMessageRepository>();
        services.AddHostedService<MongoInitializationService>();
        services.AddSingleton<IProducer<string, string>>(provider =>
        {
            var config = provider.GetRequiredService<IConfiguration>();
            return new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = config["Kafka:BootstrapServers"]
                    ?? throw new InvalidOperationException("Configure Kafka:BootstrapServers."),
                ClientId = "oficina-execution", Acks = Acks.All, EnableIdempotence = true,
                MessageTimeoutMs = config.GetValue<int?>("Kafka:MessageTimeoutMs") ?? 10_000
            }).Build();
        });
        services.AddSingleton<IMessagingService, KafkaMessagingService>();
        services.AddSingleton<ConsumerState>();
        services.Configure<HostOptions>(options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);
        services.AddHostedService<ExecutionKafkaConsumer>();
        return services;
    }
}
