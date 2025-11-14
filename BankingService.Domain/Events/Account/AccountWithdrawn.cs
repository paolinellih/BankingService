namespace BankingService.Domain.Events.Account;

public record AccountWithdrawn(Guid AccountId, decimal Amount, decimal BalanceAfter, string Currency) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
};