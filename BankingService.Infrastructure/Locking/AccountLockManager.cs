using System.Collections.Concurrent;
using BankingService.Domain.Interfaces;

namespace BankingService.Infrastructure.Locking;

public class AccountLockManager : IAccountLockManager
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    
    public async Task<IDisposable> LockAccountAsync(Guid accountId)
    {
        var semaphore = _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        return new AccountLock(semaphore);
    }

    public async Task<IDisposable> LockAccountsAsync(params Guid[] accountIds)
    {
        var orderedAccountIds = accountIds.Distinct().OrderBy(id => id).ToList();
        var semaphores = new List<SemaphoreSlim>();
        
        foreach (var accountId in orderedAccountIds)
        {
            var semaphore = _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();
            semaphores.Add(semaphore);
        }

        return new MultiAccountLock(semaphores);
    }
    
    private class AccountLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public AccountLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
    
    private class MultiAccountLock : IDisposable
    {
        private readonly List<SemaphoreSlim> _semaphores;

        public MultiAccountLock(List<SemaphoreSlim> semaphores)
        {
            _semaphores = semaphores;
        }

        public void Dispose()
        {
            foreach (var semaphore in _semaphores)
            {
                semaphore.Release();
            }
        }
    }
}