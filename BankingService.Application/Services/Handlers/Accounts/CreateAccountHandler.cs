using BankingService.Application.DTOs;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Mappers;
using BankingService.Application.Results;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BankingService.Application.Services.Handlers.Accounts;

public class CreateAccountHandler : ICreateAccountHandler
{
    private readonly ILogger<CreateAccountHandler> _logger;
    private readonly IAccountRepository _accountRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAccountHandler(
        ILogger<CreateAccountHandler> logger,
        IAccountRepository accountRepository,
        ITransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AccountDto>> HandleAsync(CreateAccountRequest request)
    {
        try
        {

            var account = new Account(
                request.AccountHolderName, 
                request.InitialDeposit,
                request.Currency,
                request.DailyWithdrawalLimit);
            _ = await _accountRepository.AddAsync(account);

            if (request.InitialDeposit > 0)
            {
                var transaction = new Transaction(
                    account.Id,
                    TransactionType.Deposit,
                    request.InitialDeposit,
                    account.Balance,
                    "Initial deposit"
                );
                
                transaction.MarkAsPosted();
                
                _ = await _transactionRepository.AddAsync(transaction);
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Account created successfully with ID: {AccountId}", account.Id);
            return Result<AccountDto>.Success(AccountMapper.ToDto(account));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error occurred while creating account for {AccountHolderName}", request.AccountHolderName);
            return Result<AccountDto>.Failure($"An error occurred while creating the account: {e.Message}");
        }
    }
}