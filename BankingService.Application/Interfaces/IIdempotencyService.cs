using BankingService.Application.Results;

namespace BankingService.Application.Interfaces;

public interface IIdempotencyService
{
    Task<Result<T>> ExecuteIdempotentOperationAsync<T>(
        string idempotencyKey, 
        string operationType, 
        Func<Task<Result<T>>> operation);
    
    Task<Result> ExecuteIdempotentOperationAsync(
        string idempotencyKey,
        string operationType,
        Func<Task<Result>> operation);
}