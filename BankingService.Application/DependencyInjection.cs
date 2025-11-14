using System.Reflection;
using BankingService.Application.Interfaces;
using BankingService.Application.Services;
using BankingService.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BankingService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register the main orchestrator
        services.AddScoped<IBankingService, Services.BankingService>();

        // Register supporting services (cross-cutting)
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        
        // Register currency conversion service
        services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
        
        // Automatically register any class that implements an interface ending with "Handler"
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Handler")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        return services;
    }
}