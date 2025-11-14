using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Results;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Accounts;

public class DeactivateAccountHandler : IDeactivateAccountHandler
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeactivateAccountHandler> _logger;

    public DeactivateAccountHandler(
        ILogger<DeactivateAccountHandler> logger, 
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result> HandleAsync(Guid accountId)
    {
        var account = await _accountRepository.GetByIdAsync(accountId);
        if (account == null)
        {
            _logger.LogWarning("Deactivate account failed: Account with ID {AccountId} not found.", accountId);
            return Result.Failure("Account not found.");
        }

        account.Deactivate();
        await _accountRepository.UpdateAsync(account);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Account with ID {AccountId} deactivated successfully.", accountId);
        return Result.Success();
    }
}