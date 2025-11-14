using BankingService.Domain.Enums;

namespace BankingService.Domain.Events.Transaction;

public record TransactionFailed(
    Guid TransactionId,
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    string Reason
) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}