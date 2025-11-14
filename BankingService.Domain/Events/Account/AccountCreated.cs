namespace BankingService.Domain.Events.Account;

public record AccountCreated(Guid AccountId, string AccountNumber, string AccountHolderName, decimal InitialBalance, string Currency) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}