using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Results;
using BankingService.Application.Services.Handlers.Deposits;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Handlers
{
    public class DepositHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<ITransactionRepository> _txRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<IAccountLockManager> _lockManagerMock = new();
        private readonly Mock<IIdempotencyService> _idempotencyServiceMock = new();
        private readonly Mock<ILogger<DepositHandler>> _loggerMock = new();
        private readonly DepositHandler _handler;

        public DepositHandlerTests()
        {
            _lockManagerMock.Setup(l => l.LockAccountAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new TestLock());

            var cache = new Dictionary<string, Result<AccountDto>>();

            _idempotencyServiceMock
                .Setup(x => x.ExecuteIdempotentOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<Result<AccountDto>>>>()))
                .Returns<string, string, Func<Task<Result<AccountDto>>>>(
                    async (key, name, func) =>
                    {
                        if (cache.ContainsKey(key))
                            return cache[key];   // Return cached result

                        var result = await func();
                        cache[key] = result;     // Store first attempt result
                        return result;
                    });

            _handler = new DepositHandler(
                _loggerMock.Object,
                _lockManagerMock.Object,
                _accountRepoMock.Object,
                _txRepoMock.Object,
                _unitOfWorkMock.Object,
                _idempotencyServiceMock.Object);
        }

        private class TestLock : IDisposable
        {
            public void Dispose() { }
        }

        private Account MakeAccount(decimal balance = 1000m) => new("Test", balance, "USD");

        [Fact]
        public async Task HandleDepositAsync_Succeeds_WhenAmountPositive()
        {
            var acc = MakeAccount();
            _accountRepoMock.Setup(r => r.GetByIdAsync(acc.Id)).ReturnsAsync(acc);

            var request = new DepositRequest(acc.Id, 200m, "KEY");

            var result = await _handler.HandleDepositAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal(acc.Balance, result.Value!.Balance);

            _txRepoMock.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Type == TransactionType.Deposit && t.Amount == 200m &&
                t.Status == TransactionStatus.Posted)), Times.Once);
            _accountRepoMock.Verify(r => r.UpdateAsync(acc), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-100)]
        public async Task HandleDepositAsync_Fails_WhenAmountNonPositive(decimal amount)
        {
            var acc = MakeAccount();
            _accountRepoMock.Setup(r => r.GetByIdAsync(acc.Id)).ReturnsAsync(acc);

            var request = new DepositRequest(acc.Id, amount, "KEY");

            var result = await _handler.HandleDepositAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Contains("must be positive", result.ErrorMessage);

            // No transaction should be created
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        }
        
        [Fact]
        public async Task Deposit_WithFailedOperation_CanBeRetriedWithSameIdempotencyKey()
        {
            var account = MakeAccount(); // some balance
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id)).ReturnsAsync(account);

            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new DepositRequest(account.Id, -100m, idempotencyKey); // invalid deposit

            var result1 = await _handler.HandleDepositAsync(request);

            Assert.False(result1.IsSuccess);

            // Retry with the same idempotency key
            var result2 = await _handler.HandleDepositAsync(request);

            // Should return the same result
            Assert.False(result2.IsSuccess);
            Assert.Equal(result1.ErrorMessage, result2.ErrorMessage);

            // Verify no transaction was created for non-positive amount
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);
        }

        [Fact]
        public async Task HandleDepositAsync_Fails_WhenAccountNotFound()
        {
            var accountId = Guid.NewGuid();
            _accountRepoMock.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync((Account?)null);

            var request = new DepositRequest(accountId, 200m, "KEY");

            var result = await _handler.HandleDepositAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Contains("Account not found", result.ErrorMessage);

            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task HandleDepositAsync_RecordsFailedTransaction_OnException()
        {
            var acc = MakeAccount();
            _accountRepoMock.Setup(r => r.GetByIdAsync(acc.Id)).ReturnsAsync(acc);
            _accountRepoMock.Setup(r => r.UpdateAsync(acc)).ThrowsAsync(new InvalidOperationException("boom"));

            var request = new DepositRequest(acc.Id, 200m, "KEY");

            var result = await _handler.HandleDepositAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Contains("boom", result.ErrorMessage);

            // Failed transaction should be recorded
            _txRepoMock.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Type == TransactionType.Deposit &&
                t.Status == TransactionStatus.Failed &&
                t.Description.Contains("Failed"))), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }
    }
}