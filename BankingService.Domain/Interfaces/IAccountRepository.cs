using BankingService.Domain.Entities;

namespace BankingService.Domain.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid accountId);
    Task<Account?> GetByAccountNumberAsync(string accountNumber);
    Task<IEnumerable<Account>> GetAllAsync();
    Task<Account> AddAsync(Account account);
    Task<bool> ExistsAsync(Guid accountId);
    Task UpdateAsync(Account account);
}