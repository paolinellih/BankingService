namespace BankingService.Application.DTOs;

public record CurrencyConversionDto(
    string FromCurrency, 
    string ToCurrency, 
    decimal Rate,
    DateTimeOffset EffectiveDate);