using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Results;

namespace BankingService.Application.Services;

public class BankingService : IBankingService
{
    private readonly IDepositHandler _depositHandler;
    private readonly IWithdrawalHandler _withdrawalHandler;
    private readonly ITransferHandler _transferHandler;
    private readonly ICreateAccountHandler _createAccountHandler;
    private readonly IGetAccountBalanceHandler _getAccountBalanceHandler;
    private readonly IGetAccountTransactionsHandler _getAccountTransactionsHandler;
    private readonly IDeactivateAccountHandler _deactivateAccountHandler;
    private readonly ITransactionReversalHandler _transactionReversalHandler;
    
    public BankingService(
        IDepositHandler depositHandler,
        IWithdrawalHandler withdrawalHandler,
        ITransferHandler transferHandler,
        ITransactionReversalHandler transactionReversalHandler, 
        ICreateAccountHandler createAccountHandler, 
        IGetAccountBalanceHandler getAccountBalanceHandler, 
        IGetAccountTransactionsHandler getAccountTransactionsHandler, 
        IDeactivateAccountHandler deactivateAccountHandler)
    {
        _depositHandler = depositHandler ?? throw new ArgumentNullException(nameof(depositHandler));
        _withdrawalHandler = withdrawalHandler ?? throw new ArgumentNullException(nameof(withdrawalHandler));
        _transferHandler = transferHandler ?? throw new ArgumentNullException(nameof(transferHandler));
        _transactionReversalHandler = transactionReversalHandler ?? throw new ArgumentNullException(nameof(transactionReversalHandler));
        _createAccountHandler = createAccountHandler ?? throw new ArgumentNullException(nameof(createAccountHandler));
        _getAccountBalanceHandler = getAccountBalanceHandler ?? throw new ArgumentNullException(nameof(getAccountBalanceHandler));
        _getAccountTransactionsHandler = getAccountTransactionsHandler ?? throw new ArgumentNullException(nameof(getAccountTransactionsHandler));
        _deactivateAccountHandler = deactivateAccountHandler ?? throw new ArgumentNullException(nameof(deactivateAccountHandler));
    }
    
    public async Task<Result<AccountDto>> CreateAccountAsync(CreateAccountRequest request)
    => await _createAccountHandler.HandleAsync(request);
    public async Task<Result<AccountDto>> DepositAsync(DepositRequest request) 
        => await _depositHandler.HandleDepositAsync(request);
    public async Task<Result<AccountDto>> WithdrawAsync(WithdrawalRequest request)
    => await _withdrawalHandler.HandleWithdrawalAsync(request);
    public async Task<Result> TransferAsync(TransferRequest request)
        => await _transferHandler.HandleTransferAsync(request);
    public async Task<Result<AccountDto>> GetAccountBalanceAsync(Guid accountId)
        => await _getAccountBalanceHandler.HandleAsync(accountId);
    public async Task<Result<IEnumerable<TransactionDto>>> GetAccountTransactionsAsync(Guid accountId)
        => await _getAccountTransactionsHandler.HandleAsync(accountId);
    public async Task<Result> DeactivateAccountAsync(Guid accountId)
        => await _deactivateAccountHandler.HandleAsync(accountId);
    public async Task<Result> ReverseTransactionAsync(Guid transactionId, string reason)
        => await _transactionReversalHandler.HandleReversalAsync(transactionId, reason);

}