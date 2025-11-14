using BankingService.Application.DTOs;
using BankingService.Application.Results;

namespace BankingService.Application.Interfaces.Handlers;

public interface ITransferHandler
{
    Task<Result> HandleTransferAsync(TransferRequest request);
}