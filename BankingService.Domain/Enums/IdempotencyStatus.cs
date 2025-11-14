namespace BankingService.Domain.Enums;

public enum IdempotencyStatus
{
    InProgress,
    Completed,
    Failed
}