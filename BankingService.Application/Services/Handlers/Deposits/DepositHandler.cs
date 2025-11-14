using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Mappers;
using BankingService.Application.Results;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Deposits;

public class DepositHandler : IDepositHandler
{
    private readonly ILogger<DepositHandler> _logger;
    private readonly IAccountLockManager _lockManager;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIdempotencyService _idempotencyService;

    public DepositHandler(
        ILogger<DepositHandler> logger,
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

    public async Task<Result<AccountDto>> HandleDepositAsync(DepositRequest request)
    {
        return await _idempotencyService.ExecuteIdempotentOperationAsync(
            request.IdempotencyKey,
            "Deposit",
            async () =>
            {
                using (await _lockManager.LockAccountAsync(request.AccountId))
                {
                    var account = await _accountRepository.GetByIdAsync(request.AccountId);
                    try
                    {
                        if (account == null)
                        {
                            _logger.LogWarning("Deposit failed: Account {AccountId} not found.", request.AccountId);
                            return Result<AccountDto>.Failure("Account not found.");
                        }
                        
                        if (request.Amount <= 0)
                        {
                            _logger.LogWarning(
                                "Deposit failed: Invalid amount {Amount} for account ID {AccountId}.",
                                request.Amount, request.AccountId);

                            return Result<AccountDto>.Failure("Transaction amount must be positive.");
                        }

                        account.Deposit(request.Amount);

                        var transaction = new Transaction(
                            account.Id,
                            TransactionType.Deposit,
                            request.Amount,
                            account.Balance,
                            "Deposit",
                            null,
                            request.IdempotencyKey
                        );

                        transaction.MarkAsPosted();

                        await _transactionRepository.AddAsync(transaction);
                        await _accountRepository.UpdateAsync(account);
                        await _unitOfWork.SaveChangesAsync();

                        _logger.LogInformation("Deposit of {Amount} to account {AccountId} successful.", request.Amount, account.Id);

                        return Result<AccountDto>.Success(AccountMapper.ToDto(account));
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
                    {
                        _logger.LogError(ex, "Deposit failed for account {AccountId}", request.AccountId);

                        // Log failed transaction
                        if (account != null)
                        {
                            var failedTransaction = new Transaction(
                                account.Id,
                                TransactionType.Deposit,
                                request.Amount,
                                account.Balance,
                                $"Failed: {ex.Message}",
                                null,
                                request.IdempotencyKey
                            );
                            failedTransaction.MarkAsFailed();
                            await _transactionRepository.AddAsync(failedTransaction);
                            await _unitOfWork.SaveChangesAsync();
                        }

                        return Result<AccountDto>.Failure(ex.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error during deposit to account {AccountId}", request.AccountId);
                        return Result<AccountDto>.Failure($"An error occurred while processing the deposit: {ex.Message}");
                    }
                }
            });
    }
}