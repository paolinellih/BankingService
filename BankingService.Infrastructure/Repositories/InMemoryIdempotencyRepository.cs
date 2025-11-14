using System.Collections.Concurrent;
using BankingService.Domain.Entities;
using BankingService.Domain.Interfaces;

namespace BankingService.Infrastructure.Repositories;

public class InMemoryIdempotencyRepository : IIdempotencyRepository
{
    private readonly ConcurrentDictionary<string, IdempotentRequest> _requests = new();
    
    public Task<IdempotentRequest?> GetByKeyAsync(string idempotencyKey)
    {
        _requests.TryGetValue(idempotencyKey, out var request);
        return Task.FromResult(request);
    }

    public Task<IdempotentRequest> AddAsync(IdempotentRequest request)
    {
        if (!_requests.TryAdd(request.IdempotencyKey, request))
            throw new InvalidOperationException("Idempotent key already exists.");
        
        return Task.FromResult(request);
    }

    public Task UpdateAsync(IdempotentRequest request)
    {
        _requests[request.IdempotencyKey] = request;
        return Task.CompletedTask;
    }
}