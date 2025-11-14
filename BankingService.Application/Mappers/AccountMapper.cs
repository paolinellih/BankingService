using BankingService.Application.DTOs;
using BankingService.Domain.Entities;

namespace BankingService.Application.Mappers;

public static class AccountMapper
{
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
            account.CountryName,
            account.TimeZone,
            account.Culture,
            account.Abbreviation
        );
    }
}