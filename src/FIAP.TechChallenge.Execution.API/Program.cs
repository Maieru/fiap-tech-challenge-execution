using System.Text;
using FIAP.TechChallenge.Execution.Application;
using FIAP.TechChallenge.Execution.Application.Messaging;
using FIAP.TechChallenge.Execution.Domain.Abstractions;
using FIAP.TechChallenge.Execution.Infrastructure;
using Maieru.FiapTechChallenge.Chassis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Scalar.AspNetCore;

namespace FIAP.TechChallenge.Execution.API;

public partial class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.AddChassis(new ChassisOptions
        {
            ServiceName = "oficina-execution",
            EnableOtlp = builder.Configuration.GetValue<bool>("Observability:EnableOtlp"),
            ActivitySourceNames = ["Fiap.TechChallenge.Execution"]
        });
        builder.Services.AddHealthChecks().AddCheck<ExecutionReadiness>("execution-dependencies", tags: ["ready"]);
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((options, configuration) =>
        {
            var issuer = configuration["Jwt:Issuer"];
            var audience = configuration["Jwt:Audience"];
            var key = configuration["Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key)
                || Encoding.UTF8.GetByteCount(key) < 32)
                throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience e Jwt:SigningKey (mínimo de 32 bytes).");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = issuer, ValidateAudience = true, ValidAudience = audience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.Zero
            };
        }).ValidateOnStart();
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseChassis();
        app.Use(async (context, next) =>
        {
            var messaging = context.RequestServices.GetRequiredService<MessageContext>();
            messaging.CorrelationId = context.Items[ChassisExtensions.CorrelationHeader]?.ToString() ?? messaging.CorrelationId;
            try { await next(context); }
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
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapChassisHealthChecks("/api/execution/v1/health");
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }
        await app.RunAsync();
    }
}
