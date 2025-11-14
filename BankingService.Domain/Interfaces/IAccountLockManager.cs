namespace BankingService.Domain.Interfaces;

public interface IAccountLockManager
{
    Task<IDisposable> LockAccountAsync(Guid accountId);
    Task<IDisposable> LockAccountsAsync(params Guid[] accountIds);
}