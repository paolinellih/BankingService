using BankingService.Application.DTOs;
using BankingService.Application.Results;

namespace BankingService.Application.Interfaces;

public interface IBankingService
{
    Task<Result<AccountDto>> CreateAccountAsync(CreateAccountRequest request);
    Task<Result<AccountDto>> DepositAsync(DepositRequest request);
    Task<Result<AccountDto>> WithdrawAsync(WithdrawalRequest request);
    Task<Result> TransferAsync(TransferRequest request);
    Task<Result<AccountDto>> GetAccountBalanceAsync(Guid accountId);
    Task<Result<IEnumerable<TransactionDto>>> GetAccountTransactionsAsync(Guid accountId);
    Task<Result> DeactivateAccountAsync(Guid accountId);
    Task<Result> ReverseTransactionAsync(Guid transactionId, string reason);
}