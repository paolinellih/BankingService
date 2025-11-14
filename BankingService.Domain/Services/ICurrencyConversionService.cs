namespace BankingService.Domain.Services;

public interface ICurrencyConversionService
{
    Task<decimal> Convert(decimal amount, string fromCurrency, string toCurrency);
    Task<decimal> GetExchangeRate(string fromCurrency, string toCurrency);
}