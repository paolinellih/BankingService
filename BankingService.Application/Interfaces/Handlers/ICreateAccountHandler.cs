using BankingService.Application.DTOs;
using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface ICreateAccountHandler
{
    Task<Result<AccountDto>> HandleAsync(CreateAccountRequest request);
}