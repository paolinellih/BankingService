using BankingService.Domain.Entities;

namespace BankingService.Domain.Interfaces;

public interface ITransactionRepository
{
    Task<Transaction> AddAsync(Transaction transaction);
    Task<IEnumerable<Transaction>> GetByAccountIdAsync(Guid accountId);
    Task AddRangeAsync(IEnumerable<Transaction> transactions);
    Task<Transaction?> GetByIdAsync(Guid transactionId);
    Task UpdateAsync(Transaction transaction);
}