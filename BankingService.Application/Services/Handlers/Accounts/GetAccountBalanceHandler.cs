using BankingService.Application.DTOs;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Mappers;
using BankingService.Application.Results;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Accounts;

public class GetAccountBalanceHandler : IGetAccountBalanceHandler
{
    private readonly ILogger<GetAccountBalanceHandler> _logger;
    private readonly IAccountRepository _accountRepository;

    public GetAccountBalanceHandler(
        ILogger<GetAccountBalanceHandler> logger,
        IAccountRepository accountRepository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    public async Task<Result<AccountDto>> HandleAsync(Guid accountId)
    {
        try
        {
            var account = await _accountRepository.GetByIdAsync(accountId);
            if (account == null)
                return Result<AccountDto>.Failure("Account not found.");

            var dto = AccountMapper.ToDto(account);

            return Result<AccountDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for account {AccountId}", accountId);
            return Result<AccountDto>.Failure($"Failed to retrieve balance: {ex.Message}");
        }
    }
}