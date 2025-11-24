using BankingService.Application.DTOs;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.ValueObjects;

namespace BankingService.Application.Mappers;

public static class AccountMapper
{
    public static AccountLocaleDTO ToLocaleDto(AccountLocale locale)
    {
        return new AccountLocaleDTO(
            locale.CountryName,
            locale.TimeZone,
            locale.Culture,
            locale.Abbreviation
        );
    }
    public static AccountDto ToDto(Account account)
    {
        return new AccountDto(
            account.Id,
            account.AccountNumber,
            account.AccountHolderName,
            account.Balance,
            account.CreatedAt,
            account.IsActive,
            account.Currency,
            ToLocaleDto(account.Locale),
            account.AccountType.ToString()
        );
    }
}