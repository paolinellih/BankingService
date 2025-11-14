using BankingService.Domain.Enums;

namespace BankingService.Domain.Events.Transaction;

public record TransactionPosted(
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    decimal BalanceAfter,
    DateTimeOffset PostedAt
) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = PostedAt;
}