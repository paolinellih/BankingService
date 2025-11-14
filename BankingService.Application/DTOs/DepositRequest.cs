namespace BankingService.Application.DTOs;

public record DepositRequest(Guid AccountId, decimal Amount, string IdempotencyKey);