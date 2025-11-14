using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Services;
using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Application.Services.Handlers.Deposits;
using BankingService.Application.Services.Handlers.Transactions;
using BankingService.Application.Services.Handlers.Transfers;
using BankingService.Application.Services.Handlers.Withdrawals;
using BankingService.Domain.Interfaces;
using BankingService.Infrastructure.Locking;
using BankingService.Infrastructure.Repositories;
using BankingService.Infrastructure.UnitOfWork;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Services;

public class IdempotencyTests
{
    private readonly IBankingService _bankingService;

    public IdempotencyTests()
    {
        // In-memory repositories
        var accountRepository = new InMemoryAccountRepository();
        var transactionRepository = new InMemoryTransactionRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var lockManager = new AccountLockManager();
        var idempotencyRepository = new InMemoryIdempotencyRepository();
        var idempotencyService = new IdempotencyService(idempotencyRepository, unitOfWork);

        // Handlers
        var depositHandler = new DepositHandler(
            Mock.Of<ILogger<DepositHandler>>(),
            lockManager,
            accountRepository,
            transactionRepository,
            unitOfWork,
            idempotencyService);

        var withdrawalHandler = new WithdrawalHandler(
            Mock.Of<ILogger<WithdrawalHandler>>(),
            lockManager,
            accountRepository,
            transactionRepository,
            unitOfWork,
            idempotencyService);

        var transferHandler = new TransferHandler(
            Mock.Of<ILogger<TransferHandler>>(),
            lockManager,
            accountRepository,
            transactionRepository,
            unitOfWork,
            idempotencyService,
            Mock.Of<Domain.Services.ICurrencyConversionService>());

        var createAccountHandler = new CreateAccountHandler(
            Mock.Of<ILogger<CreateAccountHandler>>(),
            accountRepository,
            transactionRepository,
            unitOfWork);

        var getBalanceHandler = new GetAccountBalanceHandler(Mock.Of<ILogger<GetAccountBalanceHandler>>(), accountRepository);
        var getTransactionsHandler = new GetAccountTransactionsHandler(Mock.Of<ILogger<GetAccountTransactionsHandler>>(), accountRepository, transactionRepository);
        var deactivateHandler = new DeactivateAccountHandler(Mock.Of<ILogger<DeactivateAccountHandler>>(), accountRepository, unitOfWork);
        var reversalHandler = new TransactionReversalHandler(
            accountRepository,
            transactionRepository,
            unitOfWork,
            lockManager,
            Mock.Of<ILogger<TransactionReversalHandler>>());

        // Banking service
        _bankingService = new BankingService.Application.Services.BankingService(
            depositHandler,
            withdrawalHandler,
            transferHandler,
            reversalHandler,
            createAccountHandler,
            getBalanceHandler,
            getTransactionsHandler,
            deactivateHandler);
    }

    [Fact]
    public async Task Deposit_WithSameIdempotencyKey_ShouldReturnCachedResult()
    {
        // Arrange
        var account = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("John Doe", 1000m));
        
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new DepositRequest(account.Value!.Id, 500m, idempotencyKey);

        // Act - Execute the same operation twice with same idempotency key
        var result1 = await _bankingService.DepositAsync(request);
        var result2 = await _bankingService.DepositAsync(request);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.Equal(result1.Value!.Balance, result2.Value!.Balance);

        // Verify balance was only incremented once
        var finalBalance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(1500m, finalBalance.Value!.Balance); // 1000 + 500 = 1500 (not 2000!)
    }

    [Fact]
    public async Task Withdrawal_WithSameIdempotencyKey_ShouldReturnCachedResult()
    {
        // Arrange
        var account = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("John Doe", 1000m));
        
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new WithdrawalRequest(account.Value!.Id, 300m, idempotencyKey);

        // Act - Execute twice
        var result1 = await _bankingService.WithdrawAsync(request);
        var result2 = await _bankingService.WithdrawAsync(request);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        // Verify balance was only decremented once
        var finalBalance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(700m, finalBalance.Value!.Balance); // 1000 - 300 = 700 (not 400!)
    }

    [Fact]
    public async Task Transfer_WithSameIdempotencyKey_ShouldReturnCachedResult()
    {
        // Arrange
        var account1 = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("John Doe", 1000m));
        var account2 = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("Jane Smith", 500m));
        
        var idempotencyKey = Guid.NewGuid().ToString();
        var request = new TransferRequest(
            account1.Value!.Id, 
            account2.Value!.Id, 
            400m, 
            idempotencyKey);

        // Act - Execute twice
        var result1 = await _bankingService.TransferAsync(request);
        var result2 = await _bankingService.TransferAsync(request);

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        // Verify balances changed only once
        var balance1 = await _bankingService.GetAccountBalanceAsync(account1.Value.Id);
        var balance2 = await _bankingService.GetAccountBalanceAsync(account2.Value.Id);
        
        Assert.Equal(600m, balance1.Value!.Balance);  // 1000 - 400 = 600 (not 200!)
        Assert.Equal(900m, balance2.Value!.Balance);  // 500 + 400 = 900 (not 1300!)
    }

    [Fact]
    public async Task Deposit_WithDifferentIdempotencyKeys_ShouldExecuteBoth()
    {
        // Arrange
        var account = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("John Doe", 1000m));
        
        var key1 = Guid.NewGuid().ToString();
        var key2 = Guid.NewGuid().ToString();

        // Act
        var result1 = await _bankingService.DepositAsync(
            new DepositRequest(account.Value!.Id, 200m, key1));
        var result2 = await _bankingService.DepositAsync(
            new DepositRequest(account.Value!.Id, 300m, key2));

        // Assert
        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        // Both should execute
        var finalBalance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(1500m, finalBalance.Value!.Balance); // 1000 + 200 + 300 = 1500
    }

    [Fact]
    public async Task Deposit_WithEmptyIdempotencyKey_ShouldFail()
    {
        // Arrange
        var account = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("John Doe", 1000m));
        
        var request = new DepositRequest(account.Value!.Id, 500m, "");

        // Act
        var result = await _bankingService.DepositAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Idempotency key is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ConcurrentDeposits_WithSameIdempotencyKey_ShouldExecuteOnlyOnce()
    {
        // Arrange
        var account = await _bankingService.CreateAccountAsync(
            new CreateAccountRequest("John Doe", 1000m));
        
        var idempotencyKey = Guid.NewGuid().ToString();

        // Act - Execute concurrently with same idempotency key
        var task1 = _bankingService.DepositAsync(
            new DepositRequest(account.Value!.Id, 500m, idempotencyKey));
        var task2 = _bankingService.DepositAsync(
            new DepositRequest(account.Value!.Id, 500m, idempotencyKey));

        var results = await Task.WhenAll(task1, task2);

        // Assert - One should succeed, one should return "in progress" or cached result
        var successCount = results.Count(r => r.IsSuccess);
        
        // At least one should succeed
        Assert.True(successCount >= 1);

        // Verify balance incremented only once
        var finalBalance = await _bankingService.GetAccountBalanceAsync(account.Value.Id);
        Assert.Equal(1500m, finalBalance.Value!.Balance); // Only one deposit should go through
    }
}