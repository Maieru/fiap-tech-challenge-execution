using System.Text;
using FIAP.TechChallenge.Execution.Application;
using FIAP.TechChallenge.Execution.Domain.Interfaces;
using FIAP.TechChallenge.Execution.Infrastructure;
using FIAP.TechChallenge.Execution.Infrastructure.Messaging;
using FIAP.TechChallenge.Execution.Infrastructure.Persistence;
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
        builder.AddChassis(new ChassisOptions { ServiceName = "oficina-execution",
            EnableOtlp = builder.Configuration.GetValue<bool>("Observability:EnableOtlp"),
            MeterNames = ["Fiap.TechChallenge.Execution", KafkaWorker.InstrumentationName], ActivitySourceNames = [KafkaWorker.InstrumentationName] });
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IConfiguration>((options, configuration) =>
        {
            var issuer = configuration["Jwt:Issuer"]; var audience = configuration["Jwt:Audience"];
            var key = Environment.GetEnvironmentVariable("Jwt__SigningKey");
            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
                throw new InvalidOperationException("Configure Jwt:Issuer, Jwt:Audience e Jwt:SigningKey (minimo de 32 bytes).");
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = issuer,
                ValidateAudience = true, ValidAudience = audience, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.Zero };
        }).ValidateOnStart();
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseChassis();
        app.Use(async (context, next) =>
        {
            try { await next(context); }
            catch (OperationConflictException error) { await Results.Problem(statusCode: 409, title: error.Message).ExecuteAsync(context); }
            catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
            { await Results.Problem(statusCode: 409, title: "Ja existe um cadastro ativo com estes dados.").ExecuteAsync(context); }
        });
        app.UseAuthentication(); app.UseAuthorization();
        app.MapControllers(); app.MapChassisHealthChecks("/api/execution/v1/health");
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) { app.MapOpenApi(); app.MapScalarApiReference(); }
        await app.Services.GetRequiredService<MongoBootstrap>().InitializeAsync(CancellationToken.None);
        await app.RunAsync();
    }
}
