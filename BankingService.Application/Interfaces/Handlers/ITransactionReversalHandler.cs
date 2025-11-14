using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface ITransactionReversalHandler
{
    Task<Result> HandleReversalAsync(Guid transactionId, string reason);
}