using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Results;
using BankingService.Application.Services.Handlers.Withdrawals;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Handlers
{
    public class WithdrawalHandlerTests
    {
        private readonly Mock<ILogger<WithdrawalHandler>> _loggerMock = new();
        private readonly Mock<IAccountLockManager> _lockManagerMock = new();
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<ITransactionRepository> _txRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<IIdempotencyService> _idempotencyServiceMock = new();

        private readonly WithdrawalHandler _handler;

        public WithdrawalHandlerTests()
        {
            // Setup the idempotency service to execute the operation directly
            _idempotencyServiceMock
                .Setup(x => x.ExecuteIdempotentOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<Result<AccountDto>>>>()))
                .Returns<string, string, Func<Task<Result<AccountDto>>>>((key, name, func) => func());

            // Setup lock manager to return a disposable lock
            _lockManagerMock
                .Setup(l => l.LockAccountAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Mock.Of<IDisposable>());

            _handler = new WithdrawalHandler(
                _loggerMock.Object,
                _lockManagerMock.Object,
                _accountRepoMock.Object,
                _txRepoMock.Object,
                _unitOfWorkMock.Object,
                _idempotencyServiceMock.Object);
        }

        private WithdrawalRequest MakeRequest(Guid? accountId = null, decimal amount = 100m)
        {
            return new WithdrawalRequest(
                accountId ?? Guid.NewGuid(),
                amount,
                Guid.NewGuid().ToString());
        }

        [Fact]
        public async Task HandleWithdrawalAsync_ShouldSucceed_WhenAccountAndFundsAreValid()
        {
            // Arrange
            var account = new Account("John", 500m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            var request = MakeRequest(account.Id, 200m);

            // Act
            var result = await _handler.HandleWithdrawalAsync(request);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(300m, result.Value!.Balance);

            _txRepoMock.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Type == TransactionType.Withdrawal &&
                t.Status == TransactionStatus.Posted &&
                t.Amount == 200m)), Times.Once);

            _accountRepoMock.Verify(r => r.UpdateAsync(account), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleWithdrawalAsync_ShouldFail_WhenAccountNotFound()
        {
            // Arrange
            var request = MakeRequest();
            _accountRepoMock.Setup(r => r.GetByIdAsync(request.AccountId))
                .ReturnsAsync((Account?)null);

            // Act
            var result = await _handler.HandleWithdrawalAsync(request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Account not found", result.ErrorMessage);
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);
        }

        [Fact]
        public async Task HandleWithdrawalAsync_ShouldRecordFailedTransaction_WhenInsufficientFunds()
        {
            // Arrange
            var account = new Account("Alice", 100m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            var request = MakeRequest(account.Id, 500m);

            // Act
            var result = await _handler.HandleWithdrawalAsync(request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Insufficient funds", result.ErrorMessage);

            _txRepoMock.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Status == TransactionStatus.Failed &&
                t.Description.Contains("Failed"))), Times.Once);

            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleWithdrawalAsync_ShouldFail_WhenAmountIsZeroOrNegative()
        {
            // Arrange
            var account = new Account("Bob", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            var request = new WithdrawalRequest(account.Id, 0m, "IDEMPOTENCY"); // Invalid amount

            // Act
            var result = await _handler.HandleWithdrawalAsync(request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Transaction amount must be positive", result.ErrorMessage);

            // Verify no transaction was created
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);

            // Account balance should remain unchanged
            Assert.Equal(1000m, account.Balance);
        }

        [Fact]
        public async Task HandleWithdrawalAsync_ShouldReturnFailure_WhenUnexpectedExceptionOccurs()
        {
            // Arrange
            var account = new Account("Charlie", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            _accountRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Account>()))
                .ThrowsAsync(new Exception("DB failure"));

            var request = MakeRequest(account.Id, 200m);

            // Act
            var result = await _handler.HandleWithdrawalAsync(request);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("An error occurred", result.ErrorMessage);
        }
        
        [Fact]
        public async Task Withdrawal_WithFailedOperation_CanBeRetriedWithSameIdempotencyKey()
        {
            // Arrange
            var account = new Account("Alice", 100m); // Insufficient funds
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id))
                .ReturnsAsync(account);

            var idempotencyKey = Guid.NewGuid().ToString();
            var request = new WithdrawalRequest(account.Id, 200m, idempotencyKey);

            // Mock idempotency service to actually cache the first result
            var cache = new Dictionary<string, Result<AccountDto>>();
            _idempotencyServiceMock
                .Setup(s => s.ExecuteIdempotentOperationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<Task<Result<AccountDto>>>>()))
                .Returns<string, string, Func<Task<Result<AccountDto>>>>(async (key, op, func) =>
                {
                    if (cache.TryGetValue(key, out var cached)) return cached;
                    var result = await func();
                    cache[key] = result;
                    return result;
                });

            // Act - First attempt (expected to fail)
            var result1 = await _handler.HandleWithdrawalAsync(request);

            // Assert first attempt failed
            Assert.False(result1.IsSuccess);
            Assert.Contains("Insufficient funds", result1.ErrorMessage);

            // Act - Retry with SAME idempotency key
            var result2 = await _handler.HandleWithdrawalAsync(request);

            // Assert retry returns SAME result (operation recovery)
            Assert.False(result2.IsSuccess);
            Assert.Equal(result1.ErrorMessage, result2.ErrorMessage);

            // Verify only ONE failed transaction was recorded
            _txRepoMock.Verify(r => r.AddAsync(It.Is<Transaction>(
                t => t.Status == TransactionStatus.Failed &&
                     t.IdempotencyKey == idempotencyKey
            )), Times.Once);

            // Verify SaveChanges called once
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Constructor_ShouldThrow_WhenDependenciesAreNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WithdrawalHandler(null!, _lockManagerMock.Object, _accountRepoMock.Object,
                    _txRepoMock.Object, _unitOfWorkMock.Object, _idempotencyServiceMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new WithdrawalHandler(_loggerMock.Object, null!, _accountRepoMock.Object,
                    _txRepoMock.Object, _unitOfWorkMock.Object, _idempotencyServiceMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new WithdrawalHandler(_loggerMock.Object, _lockManagerMock.Object, null!,
                    _txRepoMock.Object, _unitOfWorkMock.Object, _idempotencyServiceMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new WithdrawalHandler(_loggerMock.Object, _lockManagerMock.Object, _accountRepoMock.Object,
                    null!, _unitOfWorkMock.Object, _idempotencyServiceMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new WithdrawalHandler(_loggerMock.Object, _lockManagerMock.Object, _accountRepoMock.Object,
                    _txRepoMock.Object, null!, _idempotencyServiceMock.Object));

            Assert.Throws<ArgumentNullException>(() =>
                new WithdrawalHandler(_loggerMock.Object, _lockManagerMock.Object, _accountRepoMock.Object,
                    _txRepoMock.Object, _unitOfWorkMock.Object, null!));
        }
    }
}