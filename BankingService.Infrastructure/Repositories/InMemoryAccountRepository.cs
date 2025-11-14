using System.Collections.Concurrent;
using BankingService.Domain.Entities;
using BankingService.Domain.Interfaces;

namespace BankingService.Infrastructure.Repositories;

public class InMemoryAccountRepository : IAccountRepository
{
    private readonly ConcurrentDictionary<Guid, Account> _accounts = new();
    
    public Task<Account?> GetByIdAsync(Guid accountId)
    {
        _accounts.TryGetValue(accountId, out var account);
        return Task.FromResult(account);
    }

    public Task<Account?> GetByAccountNumberAsync(string accountNumber)
    {
        var account = _accounts.Values.FirstOrDefault(a => a.AccountNumber == accountNumber);
        return Task.FromResult(account);
    }

    public Task<IEnumerable<Account>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Account>>(_accounts.Values.ToList());
    }

    public Task<Account> AddAsync(Account account)
    {
        if (!_accounts.TryAdd(account.Id, account))
        {
            throw new InvalidOperationException("Account with the same ID already exists.");
        }

        return Task.FromResult(account);
    }

    public Task<bool> ExistsAsync(Guid accountId)
    {
        return Task.FromResult(_accounts.ContainsKey(accountId));
    }

    public Task UpdateAsync(Account account)
    {
        if(!_accounts.ContainsKey(account.Id))
        {
            throw new KeyNotFoundException("Account not found.");
        }
        _accounts[account.Id] = account;
        return Task.CompletedTask;
    }
}