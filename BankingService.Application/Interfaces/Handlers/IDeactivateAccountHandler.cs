using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface IDeactivateAccountHandler
{
    Task<Result> HandleAsync(Guid accountId);
}