using BankingService.Domain.Enums;

namespace BankingService.Domain.Events.Transaction;

public record TransactionReversed(
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    decimal BalanceAfter
) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}