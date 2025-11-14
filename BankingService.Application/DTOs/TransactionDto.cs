namespace BankingService.Application.DTOs;

public record TransactionDto(
    Guid Id,
    Guid AccountId,
    string Type,
    string Status,
    decimal Amount,
    decimal BalanceAfter,
    DateTimeOffset Timestamp,
    DateTimeOffset? PostedAt,
    string? Description,
    string? IdempotencyKey);