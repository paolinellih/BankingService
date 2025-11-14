namespace BankingService.Application.DTOs;

public record AccountDto(
    Guid Id,
    string AccountNumber,
    string AccountHolderName,
    decimal Balance,
    DateTimeOffset CreatedAt,
    bool IsActive,
    string Currency,
    string CountryName,
    string TimeZone,
    string Culture,
    string Abbreviation);