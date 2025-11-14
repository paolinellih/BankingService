namespace BankingService.Domain.Events.Account;

public record AccountDeactivated(Guid AccountId, DateTimeOffset DeactivatedAt) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; init; } = DeactivatedAt;
}