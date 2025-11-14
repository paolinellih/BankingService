namespace BankingService.Domain.Events.Account;

public record AccountDeposited(Guid AccountId, decimal Amount, decimal BalanceAfter, string Currency) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}