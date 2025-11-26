namespace BankingService.Application.DTOs;

public record AccountLocaleDTO(
    string CountryName,
    string TimeZone,
    string Culture,
    string Abbreviation);