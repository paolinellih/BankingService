using System.Text.Json;
using BankingService.Application.Interfaces;
using BankingService.Application.Results;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;

namespace BankingService.Application.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly IIdempotencyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public IdempotencyService(IIdempotencyRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }
    
    public async Task<Result<T>> ExecuteIdempotentOperationAsync<T>(
        string idempotencyKey,
        string operationType,
        Func<Task<Result<T>>> operation)
    {
        return await ExecuteIdempotentOperationInternalAsync(
            idempotencyKey,
            operationType,
            operation,
            responseData => JsonSerializer.Deserialize<Result<T>>(responseData) 
                ?? Result<T>.Failure("Failed to deserialize cached response."));
    }

    public async Task<Result> ExecuteIdempotentOperationAsync(
        string idempotencyKey,
        string operationType,
        Func<Task<Result>> operation)
    {
        return await ExecuteIdempotentOperationInternalAsync(
            idempotencyKey,
            operationType,
            operation,
            responseData => JsonSerializer.Deserialize<Result>(responseData) 
                ?? Result.Failure("Failed to deserialize cached response."));
    }

    private async Task<TResult> ExecuteIdempotentOperationInternalAsync<TResult>(
        string idempotencyKey,
        string operationType,
        Func<Task<TResult>> operation,
        Func<string, TResult> deserializeResult)
        where TResult : class
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return CreateFailure<TResult>("Idempotency key is required.");
        }

        bool lockAcquired = false;
        IdempotentRequest? idempotentRequest = null;
        
        try
        {
            await _lock.WaitAsync();
            lockAcquired = true;

            var existingRequest = await _repository.GetByKeyAsync(idempotencyKey);

            if (existingRequest != null)
            {
                if (existingRequest.Status == IdempotencyStatus.Completed)
                {
                    return deserializeResult(existingRequest.ResponseData!);
                }

                if (existingRequest.Status == IdempotencyStatus.InProgress)
                {
                    return CreateFailure<TResult>("Operation is already in progress.");
                }
            }

            idempotentRequest = new IdempotentRequest(
                idempotencyKey,
                operationType,
                JsonSerializer.Serialize(new { operationType }));

            if (existingRequest == null)
            {
                await _repository.AddAsync(idempotentRequest);
                await _unitOfWork.SaveChangesAsync();
            }
        }
        catch (InvalidOperationException)
        {
            // Race condition: another thread already added this key
            if (lockAcquired)
            {
                _lock.Release();
                lockAcquired = false;
            }

            await Task.Delay(100);
            var existingRequest = await _repository.GetByKeyAsync(idempotencyKey);

            if (existingRequest?.Status == IdempotencyStatus.Completed)
            {
                return deserializeResult(existingRequest.ResponseData!);
            }

            return CreateFailure<TResult>("Operation is being processed by another request.");
        }
        finally
        {
            // Release lock before executing operation to prevent deadlocks
            if (lockAcquired)
            {
                _lock.Release();
                lockAcquired = false;
            }
        }

        // Execute operation WITHOUT holding the idempotency lock
        try
        {
            var result = await operation();

            var responseData = JsonSerializer.Serialize(result);
            idempotentRequest!.MarkCompleted(responseData);
            await _repository.UpdateAsync(idempotentRequest);
            await _unitOfWork.SaveChangesAsync();

            return result;
        }
        catch (Exception ex)
        {
            if (idempotentRequest != null)
            {
                idempotentRequest.MarkFailed();
                await _repository.UpdateAsync(idempotentRequest);
                await _unitOfWork.SaveChangesAsync();
            }

            return CreateFailure<TResult>($"Operation failed: {ex.Message}");
        }
    }

    private static TResult CreateFailure<TResult>(string errorMessage) where TResult : class
    {
        // Use reflection to call the appropriate Failure method
        var resultType = typeof(TResult);
        
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            // Result<T>.Failure(errorMessage)
            var failureMethod = resultType.GetMethod("Failure", new[] { typeof(string) });
            return (TResult)failureMethod!.Invoke(null, new object[] { errorMessage })!;
        }
        else if (resultType == typeof(Result))
        {
            // Result.Failure(errorMessage)
            return (TResult)(object)Result.Failure(errorMessage);
        }
        
        throw new InvalidOperationException($"Unsupported result type: {resultType}");
    }
}