namespace BankingService.Application.DTOs;

public record WithdrawalRequest(Guid AccountId, decimal Amount, string IdempotencyKey);