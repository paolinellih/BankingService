using BankingService.Application.DTOs;
using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface IGetAccountBalanceHandler
{
    Task<Result<AccountDto>> HandleAsync(Guid accountId);
}