using BankingService.Domain.Entities;

namespace BankingService.Domain.Interfaces;

public interface IIdempotencyRepository
{
    Task<IdempotentRequest?> GetByKeyAsync(string idempotencyDay);
    Task<IdempotentRequest> AddAsync(IdempotentRequest request);
    Task UpdateAsync(IdempotentRequest request);
}