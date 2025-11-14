using BankingService.Application.DTOs;
using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface IGetAccountTransactionsHandler
{
    Task<Result<IEnumerable<TransactionDto>>> HandleAsync(Guid accountId);
}