namespace BankingService.ConsoleApp.Configuration;

public record CountryConfig(
    string Name,
    string Culture,
    string TimeZone,
    string Abbreviation);