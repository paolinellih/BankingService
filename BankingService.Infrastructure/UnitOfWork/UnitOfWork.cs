using BankingService.Domain.Interfaces;

namespace BankingService.Infrastructure.UnitOfWork;

public class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly SemaphoreSlim _transactionLock = new(1, 1);
    private bool _isInTrasaction = false;
    public async Task BeginTransactionAsync()
    {
        await _transactionLock.WaitAsync();
        _isInTrasaction = true;
    }

    public Task CommitAsync()
    {
        if (!_isInTrasaction)
            throw new InvalidOperationException("No active transaction to commit.");
        _isInTrasaction = false;
        _transactionLock.Release();
        return Task.CompletedTask;
    }

    public Task RollbackAsync()
    {
        if (!_isInTrasaction)
            throw new InvalidOperationException("No active transaction to rollback.");
        _isInTrasaction = false;
        _transactionLock.Release();
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return Task.CompletedTask;
    }
}