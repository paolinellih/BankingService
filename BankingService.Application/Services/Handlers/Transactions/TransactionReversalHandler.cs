using BankingService.Application.Results;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Transactions;

public class TransactionReversalHandler : ITransactionReversalHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccountLockManager _lockManager;
    private readonly ILogger<TransactionReversalHandler> _logger;

    public TransactionReversalHandler(
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IAccountLockManager lockManager,
        ILogger<TransactionReversalHandler> logger)
    {
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _lockManager = lockManager;
        _logger = logger;
    }

    public async Task<Result> HandleReversalAsync(Guid transactionId, string reason)
    {
        try
        {
            var transaction = await _transactionRepository.GetByIdAsync(transactionId);
            if (transaction == null)
                return Result.Failure("Transaction not found.");

            if (transaction.Status != TransactionStatus.Posted)
                return Result.Failure("Only posted transactions can be reversed.");

            if (transaction.Status == TransactionStatus.Reversed)
                return Result.Failure("Transaction already reversed.");

            return transaction.Type switch
            {
                TransactionType.Deposit => await ReverseDepositAsync(transaction, reason),
                TransactionType.Withdrawal => await ReverseWithdrawalAsync(transaction, reason),
                TransactionType.TransferOut or TransactionType.TransferIn => await ReverseTransferAsync(transaction, reason),
                _ => Result.Failure($"Transaction type {transaction.Type} cannot be reversed.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reversing transaction {TransactionId}", transactionId);
            return Result.Failure($"Error reversing transaction: {ex.Message}");
        }
    }

    private async Task<Result> ReverseDepositAsync(Transaction originalTransaction, string reason)
    {
        using (await _lockManager.LockAccountAsync(originalTransaction.AccountId))
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var account = await _accountRepository.GetByIdAsync(originalTransaction.AccountId);
                if (account == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return Result.Failure("Account not found.");
                }

                // Check if account has sufficient balance to reverse
                if (account.Balance < originalTransaction.Amount)
                {
                    await _unitOfWork.RollbackAsync();
                    _logger.LogWarning(
                        "Cannot reverse deposit: Insufficient balance. Required: {Required}, Available: {Available}",
                        originalTransaction.Amount, account.Balance);
                    return Result.Failure(
                        $"Insufficient balance to reverse. Need {originalTransaction.Amount:C}, have {account.Balance:C}");
                }

                // Reverse the deposit (withdraw the amount)
                account.Withdraw(originalTransaction.Amount);

                // Create reversal transaction
                var reversalTransaction = new Transaction(
                    account.Id,
                    TransactionType.Withdrawal,
                    originalTransaction.Amount,
                    account.Balance,
                    $"Reversal of deposit: {reason}",
                    null,
                    $"REVERSAL-{originalTransaction.Id}"
                );
                reversalTransaction.MarkAsPosted();

                // Mark original transaction as reversed
                originalTransaction.MarkAsReversed();

                await _transactionRepository.AddAsync(reversalTransaction);
                await _transactionRepository.UpdateAsync(originalTransaction);
                await _accountRepository.UpdateAsync(account);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Deposit transaction {TransactionId} reversed successfully.", 
                    originalTransaction.Id);
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error reversing deposit transaction {TransactionId}", 
                    originalTransaction.Id);
                await _unitOfWork.RollbackAsync();
                return Result.Failure($"Failed to reverse deposit: {e.Message}");
            }
        }
    }

    private async Task<Result> ReverseWithdrawalAsync(Transaction originalTransaction, string reason)
    {
        using (await _lockManager.LockAccountAsync(originalTransaction.AccountId))
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var account = await _accountRepository.GetByIdAsync(originalTransaction.AccountId);
                if (account == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return Result.Failure("Account not found.");
                }

                // Reverse the withdrawal (deposit the amount back)
                account.Deposit(originalTransaction.Amount);

                // Create reversal transaction
                var reversalTransaction = new Transaction(
                    account.Id,
                    TransactionType.Deposit,
                    originalTransaction.Amount,
                    account.Balance,
                    $"Reversal of withdrawal: {reason}",
                    null,
                    $"REVERSAL-{originalTransaction.Id}"
                );
                reversalTransaction.MarkAsPosted();

                // Mark original transaction as reversed
                originalTransaction.MarkAsReversed();

                await _transactionRepository.AddAsync(reversalTransaction);
                await _transactionRepository.UpdateAsync(originalTransaction);
                await _accountRepository.UpdateAsync(account);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Withdrawal transaction {TransactionId} reversed successfully.", 
                    originalTransaction.Id);
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error reversing withdrawal transaction {TransactionId}", 
                    originalTransaction.Id);
                await _unitOfWork.RollbackAsync();
                return Result.Failure($"Failed to reverse withdrawal: {e.Message}");
            }
        }
    }

    private async Task<Result> ReverseTransferAsync(Transaction originalTransaction, string reason)
    {
        if (originalTransaction.RelatedAccountId == null)
        {
            return Result.Failure("Cannot reverse transfer: Related account not found in transaction.");
        }

        // Determine which is the source and which is the destination
        Guid fromAccountId, toAccountId;
        TransactionType fromType, toType;
        
        if (originalTransaction.Type == TransactionType.TransferOut)
        {
            fromAccountId = originalTransaction.AccountId;
            toAccountId = originalTransaction.RelatedAccountId.Value;
            fromType = TransactionType.TransferOut;
            toType = TransactionType.TransferIn;
        }
        else
        {
            fromAccountId = originalTransaction.RelatedAccountId.Value;
            toAccountId = originalTransaction.AccountId;
            fromType = TransactionType.TransferOut;
            toType = TransactionType.TransferIn;
        }

        // Lock both accounts
        using (await _lockManager.LockAccountsAsync(fromAccountId, toAccountId))
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                // Get both accounts
                var fromAccount = await _accountRepository.GetByIdAsync(fromAccountId);
                var toAccount = await _accountRepository.GetByIdAsync(toAccountId);

                if (fromAccount == null || toAccount == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return Result.Failure("One or both accounts not found.");
                }

                // Get all transactions for the destination account to find the paired transaction
                var toAccountTransactions = await _transactionRepository.GetByAccountIdAsync(toAccountId);

                // Find the matching TransferIn transaction
                var pairedTransaction = toAccountTransactions.FirstOrDefault(t =>
                    t.RelatedAccountId == fromAccountId &&
                    t.Type == toType &&
                    t.IdempotencyKey == originalTransaction.IdempotencyKey && //Match using IdempotencyKey
                    t.Status == TransactionStatus.Posted);

                if (pairedTransaction == null)
                {
                    await _unitOfWork.RollbackAsync();
                    _logger.LogError("Cannot find paired transfer transaction for {TransactionId}. " +
                        "Looking for {Type} from account {RelatedAccount} with amount {Amount}",
                        originalTransaction.Id, toType, fromAccountId, originalTransaction.Amount);
                    return Result.Failure("Cannot find paired transfer transaction.");
                }

                // Check if already reversed
                if (pairedTransaction.Status == TransactionStatus.Reversed)
                {
                    await _unitOfWork.RollbackAsync();
                    return Result.Failure("Transfer has already been reversed.");
                }
                
                decimal reverseAmountFrom = pairedTransaction.Amount; // the amount that was received
                decimal reverseAmountTo = originalTransaction.Amount; // the amount that was sent

                if (toAccount.Currency != fromAccount.Currency)
                {
                    _logger.LogInformation(
                        "Reversing cross-currency transfer: from {FromCurrency} to {ToCurrency}, from ({AmountFrom} to {AmountTo})",
                        fromAccount.Currency, toAccount.Currency, reverseAmountTo, reverseAmountFrom);
                }

                // Ensure recipient has sufficient funds to reverse
                if (toAccount.Balance < reverseAmountFrom)
                {
                    await _unitOfWork.RollbackAsync();
                    _logger.LogWarning(
                        "Cannot reverse transfer: Recipient account has insufficient balance. Required: {Required}, Available: {Available}",
                        originalTransaction.Amount, toAccount.Balance);
                    return Result.Failure(
                        $"Insufficient balance in recipient account to reverse. Need {originalTransaction.Amount:C}, have {toAccount.Balance:C}");
                }

                // Reverse the transfer - money goes back to original sender
                toAccount.Withdraw(reverseAmountFrom);
                fromAccount.Deposit(reverseAmountTo);

                // Create reversal transactions
                var reversalFromTo = new Transaction(
                    toAccount.Id,
                    TransactionType.TransferOut,
                    reverseAmountFrom,
                    toAccount.Balance,
                    $"Reversal: {reason}",
                    fromAccount.Id,
                    $"REVERSAL-{pairedTransaction.Id}");

                var reversalToFrom = new Transaction(
                    fromAccount.Id,
                    TransactionType.TransferIn,
                    reverseAmountTo,
                    fromAccount.Balance,
                    $"Reversal: {reason}",
                    toAccount.Id,
                    $"REVERSAL-{originalTransaction.Id}");

                reversalFromTo.MarkAsPosted();
                reversalToFrom.MarkAsPosted();

                // Mark original transactions as reversed
                originalTransaction.MarkAsReversed();
                pairedTransaction.MarkAsReversed();

                // Save everything
                await _transactionRepository.AddAsync(reversalFromTo);
                await _transactionRepository.AddAsync(reversalToFrom);
                await _transactionRepository.UpdateAsync(originalTransaction);
                await _transactionRepository.UpdateAsync(pairedTransaction);
                await _accountRepository.UpdateAsync(fromAccount);
                await _accountRepository.UpdateAsync(toAccount);

                await _unitOfWork.CommitAsync();

                _logger.LogInformation(
                    "Reversed transfer successfully. OriginalOut={OrigId}, OriginalIn={PairedId}, Key={Key}",
                    originalTransaction.Id, pairedTransaction.Id, originalTransaction.IdempotencyKey);
                
                return Result.Success();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error reversing transfer transaction {TransactionId}", 
                    originalTransaction.Id);
                await _unitOfWork.RollbackAsync();
                return Result.Failure($"Failed to reverse transfer: {e.Message}");
            }
        }
    }
}