using BankingService.Domain.Interfaces;
using BankingService.Infrastructure.Locking;
using BankingService.Infrastructure.Repositories;
using BankingService.Infrastructure.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace BankingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAccountLockManager, AccountLockManager>();
        services.AddSingleton<IAccountRepository, InMemoryAccountRepository>();
        services.AddSingleton<IIdempotencyRepository, InMemoryIdempotencyRepository>();
        services.AddSingleton<ITransactionRepository, InMemoryTransactionRepository>();
        services.AddSingleton<IUnitOfWork, InMemoryUnitOfWork>();

        return services;
    }
}