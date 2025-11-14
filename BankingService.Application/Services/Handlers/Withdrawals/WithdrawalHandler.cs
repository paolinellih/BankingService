using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Mappers;
using BankingService.Application.Results;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Withdrawals;

public class WithdrawalHandler : IWithdrawalHandler
{
    private readonly ILogger<WithdrawalHandler> _logger;
    private readonly IAccountLockManager _lockManager;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyService _idempotencyService;

    public WithdrawalHandler(
        ILogger<WithdrawalHandler> logger,
        IAccountLockManager lockManager,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IIdempotencyService idempotencyService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _idempotencyService = idempotencyService ?? throw new ArgumentNullException(nameof(idempotencyService));
    }

    public async Task<Result<AccountDto>> HandleWithdrawalAsync(WithdrawalRequest request)
    {
        return await _idempotencyService.ExecuteIdempotentOperationAsync(
            request.IdempotencyKey,
            "Withdrawal",
            async () =>
            {
                using (await _lockManager.LockAccountAsync(request.AccountId))
                {
                    var account = await _accountRepository.GetByIdAsync(request.AccountId);
                    try
                    {
                        if (account == null)
                        {
                            _logger.LogWarning("Withdrawal failed: Account with ID {AccountId} not found.",
                                request.AccountId);
                            return Result<AccountDto>.Failure("Account not found.");
                        }
                        
                        if (request.Amount <= 0)
                        {
                            _logger.LogWarning(
                                "Withdrawal failed: Invalid amount {Amount} for account ID {AccountId}.",
                                request.Amount, request.AccountId);

                            return Result<AccountDto>.Failure("Transaction amount must be positive.");
                        }

                        account.Withdraw(request.Amount);

                        var transaction = new Transaction(
                            account.Id,
                            TransactionType.Withdrawal,
                            request.Amount,
                            account.Balance,
                            "Withdrawal"
                        );
                        
                        transaction.MarkAsPosted();

                        _ = await _transactionRepository.AddAsync(transaction);
                        await _accountRepository.UpdateAsync(account);
                        await _unitOfWork.SaveChangesAsync();

                        return Result<AccountDto>.Success(AccountMapper.ToDto(account));
                    }
                    catch (InvalidOperationException e) // Insufficient funds or inactive account
                    {
                        _logger.LogError(e, "Withdrawal failed for account ID: {AccountId}", request.AccountId);
                        
                        // Get current account state for failed transaction
                        if (account != null)
                        {
                            // Create failed transaction record
                            var failedTransaction = new Transaction(
                                account.Id,
                                TransactionType.Withdrawal,
                                request.Amount,
                                account.Balance, // Balance unchanged
                                $"Failed: {e.Message}",
                                null,
                                request.IdempotencyKey
                            );
                            
                            failedTransaction.MarkAsFailed();
                            
                            _ = await _transactionRepository.AddAsync(failedTransaction);
                            await _unitOfWork.SaveChangesAsync();
                        }
                        
                        return Result<AccountDto>.Failure(e.Message);
                    }
                    catch (ArgumentException e) // Invalid amount
                    {
                        _logger.LogError(e, "Invalid withdrawal attempt for account ID: {AccountId}", request.AccountId);
                        
                        if (account != null)
                        {
                            var failedTransaction = new Transaction(
                                account.Id,
                                TransactionType.Withdrawal,
                                request.Amount,
                                account.Balance,
                                $"Failed: {e.Message}",
                                null,
                                request.IdempotencyKey
                            );
                            
                            failedTransaction.MarkAsFailed();
                            
                            _ = await _transactionRepository.AddAsync(failedTransaction);
                            await _unitOfWork.SaveChangesAsync();
                        }
                        
                        return Result<AccountDto>.Failure(e.Message);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error occurred while withdrawing from account ID: {AccountId}",
                            request.AccountId);
                        return Result<AccountDto>.Failure(
                            $"An error occurred while processing the withdrawal: {e.Message}");
                    }
                }
            });
    }
}