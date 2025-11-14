using BankingService.Application;
using BankingService.Infrastructure;
using BankingService.ConsoleApp.Demo;
using BankingService.ConsoleApp.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BankingService.ConsoleApp;

class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();

        // Configure logging
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Warning);
        });

        // Register layers
        services.AddApplication();
        services.AddInfrastructure();

        // Register console app dependencies
        services.AddSingleton<IConsolePresenter, ConsolePresenter>();
        services.AddTransient<IBankingDemo, InternationalBankingDemo>();

        var serviceProvider = services.BuildServiceProvider();

        var demo = serviceProvider.GetRequiredService<IBankingDemo>();
        await demo.RunAsync();
    }
}