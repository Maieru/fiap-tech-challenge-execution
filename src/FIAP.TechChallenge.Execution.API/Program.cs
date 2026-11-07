using FIAP.TechChallenge.Execution.API.Security;
using FIAP.TechChallenge.Execution.Application;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Infrastructure;
using Maieru.FiapTechChallenge.Chassis;
using MongoDB.Driver;
using Scalar.AspNetCore;

namespace FIAP.TechChallenge.Execution.API;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (args.Contains("--verify-persistence", StringComparer.Ordinal))
        {
            _ = builder.Services.AddInfrastructure(builder.Configuration);
            await using var verificationApp = builder.Build();
            await FIAP.TechChallenge.Execution.Infrastructure.Persistence.PersistenceCompatibility.VerifyAsync(verificationApp.Services);
            return;
        }
        _ = builder.Services.AddControllers();
        _ = builder.Services.AddOpenApi();
        _ = builder.Services.AddApplication();
        _ = builder.Services.AddInfrastructure(builder.Configuration);
        _ = builder.AddChassis(new ChassisOptions
        {
            ServiceName = "oficina-execution",
            EnableOtlp = builder.Configuration.GetValue<bool>("Observability:EnableOtlp"),
            ActivitySourceNames = ["Fiap.TechChallenge.Execution"]
        });
        _ = builder.Services.AddHealthChecks().AddCheck<ExecutionReadiness>("execution-dependencies", tags: ["ready"]);
        _ = builder.Services.AddJwtAuthentication();
        var app = builder.Build();
        _ = app.UseChassis();
        _ = app.Use(async (context, next) =>
        {
            var messaging = context.RequestServices.GetRequiredService<MessageContext>();
            messaging.CorrelationId = context.Items[ChassisExtensions.CorrelationHeader]?.ToString() ?? messaging.CorrelationId;
            try
            { await next(context); }
            catch (ConcurrencyException)
            {
                await Results.Problem(statusCode: 409, title: "O registro foi alterado por outra operação. Consulte novamente.").ExecuteAsync(context);
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                await Results.Problem(statusCode: 409, title: "Já existe um registro com estes dados.").ExecuteAsync(context);
            }
            catch (MongoException exception) when (exception.HasErrorLabel("TransientTransactionError"))
            {
                await Results.Problem(statusCode: 409, title: "Conflito de concorrência. Consulte os dados novamente.").ExecuteAsync(context);
            }
        });
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();
        _ = app.MapControllers();
        _ = app.MapChassisHealthChecks("/api/execution/v1/health");
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            _ = app.MapOpenApi();
            _ = app.MapScalarApiReference();
        }
        await app.RunAsync();
    }
}
