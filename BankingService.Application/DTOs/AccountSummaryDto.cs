namespace BankingService.Application.DTOs;

public record AccountSummaryDto(
    Guid AccountId,
    decimal Balance,
    decimal TotalDeposits,
    decimal Withdrawals,
    string LastActivity,
    DateTimeOffset LastActivityDate);