using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace FIAP.TechChallenge.Execution.API.Security;

public static class AuthenticationDependencyInjection
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        _ = services.AddSingleton(provider =>
        {
            var signingKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
            if (string.IsNullOrWhiteSpace(signingKey) || Encoding.UTF8.GetByteCount(signingKey) < 32)
                throw new InvalidOperationException("Configure a variável Jwt__SigningKey (mínimo de 32 bytes).");

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        });
        _ = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        _ = services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration, SymmetricSecurityKey>((options, configuration, signingKey) =>
            {
                var jwtSection = configuration.GetSection("Jwt");
                var issuer = jwtSection["Issuer"];
                var audience = jwtSection["Audience"];
                if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
                    throw new InvalidOperationException("Configure Jwt:Issuer e Jwt:Audience.");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.Zero
                };
            }).ValidateOnStart();
        _ = services.AddAuthorization();
        return services;
    }
}
