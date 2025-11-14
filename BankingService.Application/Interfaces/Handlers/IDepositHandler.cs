using BankingService.Application.DTOs;
using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface IDepositHandler
{
    Task<Result<AccountDto>> HandleDepositAsync(DepositRequest request);
}