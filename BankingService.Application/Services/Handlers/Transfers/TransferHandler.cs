using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Results;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using BankingService.Domain.Services;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Transfers;

public class TransferHandler : ITransferHandler
{
    private readonly ILogger<TransferHandler> _logger;
    private readonly IAccountLockManager _lockManager;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyService _idempotencyService;
    private readonly ICurrencyConversionService _currencyConversionService;

    public TransferHandler(
        ILogger<TransferHandler> logger,
        IAccountLockManager lockManager,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IIdempotencyService idempotencyService, 
        ICurrencyConversionService currencyConversionService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
        _currencyConversionService = currencyConversionService ?? throw new ArgumentNullException(nameof(currencyConversionService));
    }
    
    public async Task<Result> HandleTransferAsync(TransferRequest request)
    {
        return await _idempotencyService.ExecuteIdempotentOperationAsync(
            request.IdempotencyKey,
            "Transfer",
            async () =>
            {
                if (request.FromAccountId == request.ToAccountId)
                {
                    return Result.Failure("Cannot transfer to the same account.");
                }
                using (await _lockManager.LockAccountsAsync(request.FromAccountId, request.ToAccountId))
                {
                    try
                    {
                        await _unitOfWork.BeginTransactionAsync();

                        var fromAccount = await _accountRepository.GetByIdAsync(request.FromAccountId);
                        if (fromAccount == null)
                        {
                            await _unitOfWork.RollbackAsync();
                            _logger.LogWarning("Transfer failed: Source account with ID {FromAccountId} not found.",
                                request.FromAccountId);
                            return Result.Failure("Source account not found.");
                        }

                        var toAccount = await _accountRepository.GetByIdAsync(request.ToAccountId);
                        if (toAccount == null)
                        {
                            await _unitOfWork.RollbackAsync();
                            _logger.LogWarning("Transfer failed: Destination account with ID {ToAccountId} not found.",
                                request.ToAccountId);
                            return Result.Failure("Destination account not found.");
                        }

                        if (fromAccount.Id == toAccount.Id)
                        {
                            await _unitOfWork.RollbackAsync();
                            _logger.LogWarning(
                                "Transfer failed: Source and destination accounts are the same (ID: {AccountId}).",
                                request.FromAccountId);
                            return Result.Failure("Source and destination accounts cannot be the same.");
                        }
                        
                        if (!fromAccount.IsActive || !toAccount.IsActive)
                        {
                            await _unitOfWork.RollbackAsync();
                            return Result.Failure("Cannot transfer involving an inactive account.");
                        }

                        decimal transferAmountToCredit = request.Amount;
                        decimal exchangeRate = 1m;
                        
                        if(!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
                        {
                            exchangeRate = await _currencyConversionService.GetExchangeRate(fromAccount.Currency, toAccount.Currency);
                            transferAmountToCredit = request.Amount * exchangeRate;
                        }

                        // Perform transfer
                        fromAccount.Withdraw(request.Amount);
                        toAccount.Deposit(transferAmountToCredit);

                        // Create transactions records
                        var transactions = new[]
                        {
                            new Transaction(
                                fromAccount.Id,
                                TransactionType.TransferOut,
                                request.Amount,
                                fromAccount.Balance,
                                $"Transfer to {toAccount.AccountNumber} ({toAccount.Currency})",
                                toAccount.Id,
                                request.IdempotencyKey),
                            
                            new Transaction(
                                toAccount.Id,
                                TransactionType.TransferIn,
                                transferAmountToCredit,
                                toAccount.Balance,
                                $"Transfer from {fromAccount.AccountNumber} ({fromAccount.Currency}) [Rate: {exchangeRate}]",
                                fromAccount.Id,
                                request.IdempotencyKey)
                        };

                        foreach (var transaction in transactions)
                        {
                            transaction.MarkAsPosted();
                        }

                        await _transactionRepository.AddRangeAsync(transactions);
                        await _accountRepository.UpdateAsync(fromAccount);
                        await _accountRepository.UpdateAsync(toAccount);

                        await _unitOfWork.CommitAsync();
                        
                        _logger.LogInformation(
                            "Transfer succeeded: {Amount}{FromCurrency} ({FromAccount}) to {AmountToCredit}{ToCurrency} ({ToAccount})",
                            request.Amount, fromAccount.Currency, fromAccount.AccountNumber,
                            transferAmountToCredit, toAccount.Currency, toAccount.AccountNumber);

                        return Result.Success();
                    }
                    catch (InvalidOperationException e) // Insufficient funds or inactive account
                    {
                        _logger.LogError(e,
                            "Transfer failed from account ID: {FromAccountId} to account ID: {ToAccountId}",
                            request.FromAccountId, request.ToAccountId);
                        await _unitOfWork.RollbackAsync();
                        
                        // Record failed transaction on source account
                        var fromAccount = await _accountRepository.GetByIdAsync(request.FromAccountId);
                        if (fromAccount != null)
                        {
                            var failedTransaction = new Transaction(
                                fromAccount.Id,
                                TransactionType.TransferOut,
                                request.Amount,
                                fromAccount.Balance,
                                $"Failed: {e.Message}",
                                request.ToAccountId,
                                request.IdempotencyKey
                            );
                            
                            failedTransaction.MarkAsFailed();
                            
                            _ = await _transactionRepository.AddAsync(failedTransaction);
                            await _unitOfWork.SaveChangesAsync();
                        }
                        
                        return Result.Failure(e.Message);
                    }
                    catch (ArgumentException e)
                    {
                        _logger.LogError(e,
                            "Invalid transfer attempt from account ID: {FromAccountId} to account ID: {ToAccountId}",
                            request.FromAccountId, request.ToAccountId);
                        await _unitOfWork.RollbackAsync();
                        
                        var fromAccount = await _accountRepository.GetByIdAsync(request.FromAccountId);
                        if (fromAccount != null)
                        {
                            var failedTransaction = new Transaction(
                                fromAccount.Id,
                                TransactionType.TransferOut,
                                request.Amount,
                                fromAccount.Balance,
                                $"Failed: {e.Message}",
                                request.ToAccountId,
                                request.IdempotencyKey
                            );
                            
                            failedTransaction.MarkAsFailed();
                            
                            _ = await _transactionRepository.AddAsync(failedTransaction);
                            await _unitOfWork.SaveChangesAsync();
                        }
                        
                        return Result.Failure(e.Message);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e,
                            "Error occurred while transferring from account ID: {FromAccountId} to account ID: {ToAccountId}",
                            request.FromAccountId, request.ToAccountId);
                        await _unitOfWork.RollbackAsync();
                        return Result.Failure($"An error occurred while processing the transfer: {e.Message}");
                    }
                }
            });
    }
}