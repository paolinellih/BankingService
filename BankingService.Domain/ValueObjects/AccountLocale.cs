namespace BankingService.Domain.ValueObjects;

public record AccountLocale(
    string CountryName,
    string TimeZone,
    string Culture,
    string Abbreviation);