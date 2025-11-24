
namespace BankingService.Application.DTOs;

public record AccountDto(
    Guid Id,
    string AccountNumber,
    string AccountHolderName,
    decimal Balance,
    DateTimeOffset CreatedAt,
    bool IsActive,
    string Currency,
    AccountLocaleDTO Locale,
    string AccountType);