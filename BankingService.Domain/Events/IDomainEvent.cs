namespace BankingService.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}