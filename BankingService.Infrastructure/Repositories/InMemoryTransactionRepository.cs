using System.Collections.Concurrent;
using BankingService.Domain.Entities;
using BankingService.Domain.Interfaces;

namespace BankingService.Infrastructure.Repositories;

public class InMemoryTransactionRepository : ITransactionRepository
{
    private readonly ConcurrentDictionary<Guid, Transaction> _transactions = new();
    public Task<Transaction> AddAsync(Transaction transaction)
    {
        if (!_transactions.TryAdd(transaction.Id, transaction))
            throw new InvalidOperationException("Transaction with the same ID already exists.");
        
        return Task.FromResult(transaction);
    }

    public Task<IEnumerable<Transaction>> GetByAccountIdAsync(Guid accountId)
    {
        var transactions = _transactions.Values.Where(t => t.AccountId == accountId).ToList();
        return Task.FromResult<IEnumerable<Transaction>>(transactions);
    }

    public Task AddRangeAsync(IEnumerable<Transaction> transactions)
    {
        foreach (var transaction in transactions)
        {
            if (!_transactions.TryAdd(transaction.Id, transaction))
                throw new InvalidOperationException("Transaction with the same ID already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Transaction?> GetByIdAsync(Guid transactionId)
    {
        _transactions.TryGetValue(transactionId, out var transaction);
        return Task.FromResult(transaction);
    }

    public Task UpdateAsync(Transaction transaction)
    {
        if (!_transactions.ContainsKey(transaction.Id))
            throw new KeyNotFoundException("Transaction not found.");
        _transactions[transaction.Id] = transaction;
        return Task.CompletedTask;
    }
}