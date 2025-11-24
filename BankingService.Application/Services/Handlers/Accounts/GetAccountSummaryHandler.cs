using BankingService.Application.DTOs;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Results;
using BankingService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Accounts;

public class GetAccountSummaryHandler : IGetAccountSummaryHandler
{
    private readonly ILogger<GetAccountSummaryHandler> _logger;
    private readonly IGetAccountTransactionsHandler _getAccountTransactionsHandler;

    public GetAccountSummaryHandler(ILogger<GetAccountSummaryHandler> logger, IGetAccountTransactionsHandler getAccountTransactionsHandler)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getAccountTransactionsHandler = getAccountTransactionsHandler ?? throw new ArgumentNullException(nameof(getAccountTransactionsHandler));
    }

    public async Task<Result<AccountSummaryDto>> HandleAsync(Guid accountId)
    {
        try
        {
            var txResult = await _getAccountTransactionsHandler.HandleAsync(accountId);
            
            if (!txResult.IsSuccess)
            {
                _logger.LogError("Failed to retrieve transactions for account {AccountId}: {Error}", accountId, txResult.ErrorMessage);
                return Result<AccountSummaryDto>.Failure(txResult.ErrorMessage);
            }
            
            // Aggregate transactions and compute totals
            var txs = txResult.Value ?? Enumerable.Empty<TransactionDto>();
            var lastPostedTx = txs.Where(t => t.PostedAt != null).OrderByDescending(t => t.PostedAt).FirstOrDefault();
            
            return Result<AccountSummaryDto>.Success(
                new AccountSummaryDto(
                    AccountId: accountId,
                    Balance: lastPostedTx?.BalanceAfter ?? 0m,
                    TotalDeposits: txResult.Value?.Where(t => t.Type == TransactionType.Deposit.ToString()).Sum(t => t.Amount) ?? 0m,
                    Withdrawals: txResult.Value?.Where(t => t.Type == TransactionType.Withdrawal.ToString()).Sum(t => t.Amount) ?? 0m,
                    LastActivity: lastPostedTx?.Type ?? "No Activity",
                    LastActivityDate: lastPostedTx?.PostedAt ?? DateTimeOffset.MinValue
                )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while retrieving account summary for account {AccountId}", accountId);
            return Result<AccountSummaryDto>.Failure($"Error occurred while retrieve summary: {ex.Message}.");
        }
    }
}