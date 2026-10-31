using FIAP.TechChallenge.Execution.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace FIAP.TechChallenge.Execution.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        foreach (var type in typeof(ApplicationDependencyInjection).Assembly.GetTypes().Where(x => x.IsClass && !x.IsAbstract && x.Name.EndsWith("UseCase")))
            foreach (var contract in type.GetInterfaces()) services.AddScoped(contract, type);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IdempotentCommandRunner>();
        services.AddScoped<ExecucaoUseCases>();
        services.AddScoped<ProcessarEventoUseCase>();
        return services;
    }
}
