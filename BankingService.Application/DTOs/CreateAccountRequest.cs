namespace BankingService.Application.DTOs;

public record CreateAccountRequest(
    string AccountHolderName,
    decimal InitialDeposit,
    string Currency = "USD",
    decimal DailyWithdrawalLimit = 1000m,
    string CountryName = "USA",
    string TimeZone = "Eastern Standard Time",
    string Culture = "en-US",
    string Abbreviation = "EST"
);
