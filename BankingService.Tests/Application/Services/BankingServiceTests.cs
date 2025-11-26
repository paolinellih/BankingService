using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Services;
using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Application.Services.Handlers.Deposits;
using BankingService.Application.Services.Handlers.Transfers;
using BankingService.Application.Services.Handlers.Withdrawals;
using BankingService.Application.Services.Handlers.Transactions;
using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Domain.Interfaces;
using BankingService.Domain.Services;
using BankingService.Infrastructure.Locking;
using BankingService.Infrastructure.Repositories;
using BankingService.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Services;

public class BankingServiceTests
{
    private readonly IBankingService _bankingService;

    public BankingServiceTests()
    {
        // In-memory repositories
        var accountRepo = new InMemoryAccountRepository();
        var transactionRepo = new InMemoryTransactionRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var lockManager = new AccountLockManager();
        var idempotencyRepo = new InMemoryIdempotencyRepository();
        var idempotencyService = new IdempotencyService(idempotencyRepo, unitOfWork);
        var currencyService = Mock.Of<ICurrencyConversionService>();

        // Handlers
        var depositHandler = new DepositHandler(Mock.Of<ILogger<DepositHandler>>(), lockManager, accountRepo, transactionRepo, unitOfWork, idempotencyService);
        var withdrawalHandler = new WithdrawalHandler(Mock.Of<ILogger<WithdrawalHandler>>(), lockManager, accountRepo, transactionRepo, unitOfWork, idempotencyService);
        var transferHandler = new TransferHandler(Mock.Of<ILogger<TransferHandler>>(), lockManager, accountRepo, transactionRepo, unitOfWork, idempotencyService, currencyService);
        var createAccountHandler = new CreateAccountHandler(Mock.Of<ILogger<CreateAccountHandler>>(), accountRepo, transactionRepo, unitOfWork);
        var getBalanceHandler = new GetAccountBalanceHandler(Mock.Of<ILogger<GetAccountBalanceHandler>>(), accountRepo);
        var getTransactionsHandler = new GetAccountTransactionsHandler(Mock.Of<ILogger<GetAccountTransactionsHandler>>(), accountRepo, transactionRepo);
        var deactivateHandler = new DeactivateAccountHandler(Mock.Of<ILogger<DeactivateAccountHandler>>(), accountRepo, unitOfWork);
        var reversalHandler = new TransactionReversalHandler(accountRepo, transactionRepo, unitOfWork, lockManager, Mock.Of<ILogger<TransactionReversalHandler>>());
        var getAccountSummaryHandler = new GetAccountSummaryHandler(Mock.Of<ILogger<GetAccountSummaryHandler>>(), getTransactionsHandler);

        // Banking service
        _bankingService = new BankingService.Application.Services.BankingService(
            depositHandler,
            withdrawalHandler,
            transferHandler,
            reversalHandler,
            createAccountHandler,
            getBalanceHandler,
            getTransactionsHandler,
            deactivateHandler,
            getAccountSummaryHandler
        );
    }

    [Fact]
    public async Task CreateAccount_ShouldSucceed()
    {
        var result = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Alice", 1000m));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Alice", result.Value!.AccountHolderName);
        Assert.Equal(1000m, result.Value.Balance);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task Deposit_ShouldIncreaseBalance()
    {
        var account = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Bob", 1000m));

        var result = await _bankingService.DepositAsync(new DepositRequest(account.Value!.Id, 500m, Guid.NewGuid().ToString()));

        Assert.True(result.IsSuccess);
        var balance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(1500m, balance.Value!.Balance);
    }

    [Fact]
    public async Task Withdrawal_ShouldDecreaseBalance()
    {
        var account = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Charlie", 1000m));

        var result = await _bankingService.WithdrawAsync(new WithdrawalRequest(account.Value!.Id, 400m, Guid.NewGuid().ToString()));

        Assert.True(result.IsSuccess);
        var balance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(600m, balance.Value!.Balance);
    }

    [Fact]
    public async Task Transfer_ShouldUpdateBothAccounts()
    {
        var from = await _bankingService.CreateAccountAsync(new CreateAccountRequest("David", 1000m));
        var to = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Eve", 500m));

        var result = await _bankingService.TransferAsync(new TransferRequest(from.Value!.Id, to.Value!.Id, 300m, Guid.NewGuid().ToString()));

        Assert.True(result.IsSuccess);

        var fromBalance = await _bankingService.GetAccountBalanceAsync(from.Value.Id);
        var toBalance = await _bankingService.GetAccountBalanceAsync(to.Value.Id);

        Assert.Equal(700m, fromBalance.Value!.Balance);
        Assert.Equal(800m, toBalance.Value!.Balance);
    }

    [Fact]
    public async Task ReverseDeposit_ShouldDecreaseBalance()
    {
        var account = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Frank", 1000m));
        var depositKey = Guid.NewGuid().ToString();

        var deposit = await _bankingService.DepositAsync(new DepositRequest(account.Value!.Id, 500m, depositKey));
        var transactions = await _bankingService.GetAccountTransactionsAsync(account.Value!.Id);
        var depositTx = transactions.Value!.First(t => t.Amount == 500m);

        var reversal = await _bankingService.ReverseTransactionAsync(depositTx.Id, "Test reversal");

        Assert.True(reversal.IsSuccess);

        var balance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(1000m, balance.Value!.Balance);
    }

    [Fact]
    public async Task ReverseWithdrawal_ShouldIncreaseBalance()
    {
        var account = await _bankingService.CreateAccountAsync(new CreateAccountRequest("George", 1000m));
        var withdrawKey = Guid.NewGuid().ToString();

        await _bankingService.WithdrawAsync(new WithdrawalRequest(account.Value!.Id, 300m, withdrawKey));
        var transactions = await _bankingService.GetAccountTransactionsAsync(account.Value!.Id);
        var withdrawalTx = transactions.Value!.First(t => t.Amount == 300m && t.Type == Domain.Enums.TransactionType.Withdrawal.ToString());

        var reversal = await _bankingService.ReverseTransactionAsync(withdrawalTx.Id, "Test reversal");

        Assert.True(reversal.IsSuccess);

        var balance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(1000m, balance.Value!.Balance);
    }

    [Fact]
    public async Task ReverseTransfer_ShouldUpdateBothAccounts()
    {
        var from = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Hannah", 1000m));
        var to = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Ian", 500m));

        var key = Guid.NewGuid().ToString();
        await _bankingService.TransferAsync(new TransferRequest(from.Value!.Id, to.Value!.Id, 400m, key));

        var fromTx = (await _bankingService.GetAccountTransactionsAsync(from.Value.Id)).Value!.First(t => t.Type == Domain.Enums.TransactionType.TransferOut.ToString());
        var reversal = await _bankingService.ReverseTransactionAsync(fromTx.Id, "Reversal test");

        Assert.True(reversal.IsSuccess);

        var fromBalance = await _bankingService.GetAccountBalanceAsync(from.Value.Id);
        var toBalance = await _bankingService.GetAccountBalanceAsync(to.Value.Id);

        Assert.Equal(1000m, fromBalance.Value!.Balance);
        Assert.Equal(500m, toBalance.Value!.Balance);
    }

    [Fact]
    public async Task DeactivateAccount_ShouldPreventOperations()
    {
        var account = await _bankingService.CreateAccountAsync(new CreateAccountRequest("Jack", 1000m));

        var deactivate = await _bankingService.DeactivateAccountAsync(account.Value!.Id);
        Assert.True(deactivate.IsSuccess);

        var deposit = await _bankingService.DepositAsync(new DepositRequest(account.Value!.Id, 100m, Guid.NewGuid().ToString()));
        Assert.False(deposit.IsSuccess);

        var withdraw = await _bankingService.WithdrawAsync(new WithdrawalRequest(account.Value!.Id, 100m, Guid.NewGuid().ToString()));
        Assert.False(withdraw.IsSuccess);
    }
}