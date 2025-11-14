using BankingService.Domain.Services;

namespace BankingService.Application.Services;

public class CurrencyConversionService : ICurrencyConversionService
{
    // Simple in-memory table — for demo purposes
    private static readonly Dictionary<(string, string), decimal> ExchangeRates = new()
    {
        { ("USD", "GBP"), 0.79m },
        { ("GBP", "USD"), 1.26m },
        { ("USD", "USD"), 1.00m },
        { ("GBP", "GBP"), 1.00m }
    };
    
    public async Task<decimal> Convert(decimal amount, string fromCurrency, string toCurrency)
    {
        var rate = await GetExchangeRate(fromCurrency, toCurrency);
        return Math.Round(amount * rate, 0);
    }

    public async Task<decimal> GetExchangeRate(string fromCurrency, string toCurrency)
    {
        if (ExchangeRates.TryGetValue((fromCurrency, toCurrency), out var rate))
            return rate;
        
        throw new InvalidOperationException($"Exchange rate from {fromCurrency} to {toCurrency} not found.");
    }
}