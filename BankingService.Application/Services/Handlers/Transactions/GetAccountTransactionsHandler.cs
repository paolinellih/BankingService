using BankingService.Application.DTOs;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Mappers;
using BankingService.Application.Results;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Transactions;

public class GetAccountTransactionsHandler : IGetAccountTransactionsHandler
{
    private readonly ILogger<GetAccountTransactionsHandler> _logger;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;

    public GetAccountTransactionsHandler(
        ILogger<GetAccountTransactionsHandler> logger,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
    }

    public async Task<Result<IEnumerable<TransactionDto>>> HandleAsync(Guid accountId)
    {
        try
        {
            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account is null)
            {
                _logger.LogWarning("Get transactions failed: Account with ID {AccountId} not found.", accountId);
                return Result<IEnumerable<TransactionDto>>.Failure("Account not found.");
            }

            var transactions = await _transactionRepository.GetByAccountIdAsync(accountId);

            var dtos = transactions
                .OrderByDescending(t => t.Timestamp)
                .ThenByDescending(t => t.PostedAt)
                .Select(TransactionMapper.ToDto)
                .ToList();

            return Result<IEnumerable<TransactionDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while retrieving transactions for account ID: {AccountId}", accountId);
            return Result<IEnumerable<TransactionDto>>.Failure($"An error occurred while retrieving transactions: {ex.Message}");
        }
    }
}