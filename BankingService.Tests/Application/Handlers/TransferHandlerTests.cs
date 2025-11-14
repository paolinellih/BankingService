using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Results;
using BankingService.Application.Services.Handlers.Transfers;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using BankingService.Domain.Services;
using Moq;
using Microsoft.Extensions.Logging;

namespace BankingService.Tests.Application.Handlers
{
    public class TransferHandlerTests
    {
        private readonly Mock<ILogger<TransferHandler>> _logger = new();
        private readonly Mock<IAccountLockManager> _lockManager = new();
        private readonly Mock<IAccountRepository> _accountRepo = new();
        private readonly Mock<ITransactionRepository> _txRepo = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IIdempotencyService> _idempotency = new();
        private readonly Mock<ICurrencyConversionService> _fx = new();

        private readonly TransferHandler _handler;

        public TransferHandlerTests()
        {
            // Idempotency simply invokes the provided delegate (simulate no caching)
            _idempotency
                .Setup(s => s.ExecuteIdempotentOperationAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<Result>>>()))
                .Returns<string, string, Func<Task<Result>>>((k, t, fn) => fn());

            // Simple disposable returned by lock manager
            _lockManager
                .Setup(l => l.LockAccountsAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new TestLock());

            _handler = new TransferHandler(
                _logger.Object,
                _lockManager.Object,
                _accountRepo.Object,
                _txRepo.Object,
                _uow.Object,
                _idempotency.Object,
                _fx.Object);
        }

        private class TestLock : IDisposable
        {
            public void Dispose() { /* no-op */ }
        }

        private TransferRequest MakeRequest(Guid from, Guid to, decimal amount)
            => new TransferRequest(from, to, amount, Guid.NewGuid().ToString());

        [Fact]
        public void Constructor_ShouldThrow_WhenDependenciesNull()
        {
            // logger
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(null!, _lockManager.Object, _accountRepo.Object,
                    _txRepo.Object, _uow.Object, _idempotency.Object, _fx.Object));

            // lock manager
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(_logger.Object, null!, _accountRepo.Object,
                    _txRepo.Object, _uow.Object, _idempotency.Object, _fx.Object));

            // account repo
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(_logger.Object, _lockManager.Object, null!,
                    _txRepo.Object, _uow.Object, _idempotency.Object, _fx.Object));

            // tx repo
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(_logger.Object, _lockManager.Object, _accountRepo.Object,
                    null!, _uow.Object, _idempotency.Object, _fx.Object));

            // unit of work
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(_logger.Object, _lockManager.Object, _accountRepo.Object,
                    _txRepo.Object, null!, _idempotency.Object, _fx.Object));

            // idempotency
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(_logger.Object, _lockManager.Object, _accountRepo.Object,
                    _txRepo.Object, _uow.Object, null!, _fx.Object));

            // fx service
            Assert.Throws<ArgumentNullException>(() =>
                new TransferHandler(_logger.Object, _lockManager.Object, _accountRepo.Object,
                    _txRepo.Object, _uow.Object, _idempotency.Object, null!));
        }

        [Fact]
        public async Task HandleTransferAsync_Succeeds_SameCurrency()
        {
            // Arrange - both USD
            var from = new Account("From", 1000m, "USD");
            var to = new Account("To", 500m, "USD");

            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            var req = MakeRequest(from.Id, to.Id, 300m);

            // Act
            var result = await _handler.HandleTransferAsync(req);

            // Assert
            Assert.True(result.IsSuccess);

            // from decreased by 300, to increased by 300
            _accountRepo.Verify(r => r.UpdateAsync(It.Is<Account>(a => a.Id == from.Id && a.Balance == 700m)), Times.Once);
            _accountRepo.Verify(r => r.UpdateAsync(It.Is<Account>(a => a.Id == to.Id && a.Balance == 800m)), Times.Once);

            _txRepo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<Transaction>>(txs =>
                txs.Count() == 2 &&
                txs.Any(t => t.Type == TransactionType.TransferOut && t.Amount == 300m) &&
                txs.Any(t => t.Type == TransactionType.TransferIn && t.Amount == 300m)
            )), Times.Once);

            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleTransferAsync_Succeeds_CrossCurrency_UsesConversionRate()
        {
            // Arrange - GBP -> USD
            var from = new Account("From", 1000m, "GBP");
            var to = new Account("To", 500m, "USD");

            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            // Mock FX rate: 1 GBP = 1.30 USD
            _fx.Setup(f => f.GetExchangeRate("GBP", "USD")).ReturnsAsync(1.30m);

            var req = MakeRequest(from.Id, to.Id, 200m);

            // Act
            var result = await _handler.HandleTransferAsync(req);

            // Assert
            Assert.True(result.IsSuccess);

            // from decreased by 200 GBP
            _accountRepo.Verify(r => r.UpdateAsync(It.Is<Account>(a => a.Id == from.Id && a.Balance == 800m)), Times.Once);

            // to increased by 200 * 1.30 = 260 USD
            _accountRepo.Verify(r => r.UpdateAsync(It.Is<Account>(a => a.Id == to.Id && a.Balance == 760m)), Times.Once);

            _txRepo.Verify(r => r.AddRangeAsync(It.Is<IEnumerable<Transaction>>(txs =>
                txs.Any(t => t.Type == TransactionType.TransferOut && t.Amount == 200m) &&
                txs.Any(t => t.Type == TransactionType.TransferIn && t.Amount == 260m)
            )), Times.Once);

            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleTransferAsync_Fails_WhenFromAccountNotFound()
        {
            // Arrange
            var fromId = Guid.NewGuid();
            var to = new Account("To", 100m);
            _accountRepo.Setup(r => r.GetByIdAsync(fromId)).ReturnsAsync((Account?)null);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            var req = MakeRequest(fromId, to.Id, 50m);

            // Act
            var result = await _handler.HandleTransferAsync(req);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Source account not found", result.ErrorMessage);

            _uow.Verify(u => u.RollbackAsync(), Times.Once);
            _txRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Never);
        }

        [Fact]
        public async Task HandleTransferAsync_Fails_WhenToAccountNotFound()
        {
            // Arrange
            var from = new Account("From", 200m);
            var missingToId = Guid.NewGuid();
            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(missingToId)).ReturnsAsync((Account?)null);

            var req = MakeRequest(from.Id, missingToId, 50m);

            // Act
            var result = await _handler.HandleTransferAsync(req);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Destination account not found", result.ErrorMessage);

            _uow.Verify(u => u.RollbackAsync(), Times.Once);
            _txRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Never);
        }

        [Fact]
        public async Task HandleTransferAsync_Fails_WhenSameAccount()
        {
            var acc = new Account("Same", 1000m);
            var req = MakeRequest(acc.Id, acc.Id, 100m);

            var res = await _handler.HandleTransferAsync(req);

            Assert.False(res.IsSuccess);
            Assert.Contains("to the same account", res.ErrorMessage);

            _txRepo.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()), Times.Never);
        }

        [Fact]
        public async Task HandleTransferAsync_Fails_WhenEitherAccountInactive()
        {
            // Arrange: from inactive
            var from = new Account("From", 1000m);
            var to = new Account("To", 100m);
            from.Deactivate();

            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            var req = MakeRequest(from.Id, to.Id, 50m);

            var res = await _handler.HandleTransferAsync(req);

            Assert.False(res.IsSuccess);
            Assert.Contains("inactive account", res.ErrorMessage);
            _uow.Verify(u => u.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleTransferAsync_RecordsFailedTransaction_OnInsufficientFunds()
        {
            // Arrange
            var from = new Account("From", 100m, "USD");
            var to = new Account("To", 100m, "USD");

            _accountRepo.SetupSequence(r => r.GetByIdAsync(from.Id))
                .ReturnsAsync(from)   // first call inside try
                .ReturnsAsync(from);  // second call inside catch when recording failed tx

            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            var req = MakeRequest(from.Id, to.Id, 500m); // greater than balance -> withdraw throws

            // Act
            var res = await _handler.HandleTransferAsync(req);

            // Assert
            Assert.False(res.IsSuccess);
            Assert.Contains("Insufficient funds", res.ErrorMessage);

            // Verify failed transaction was attempted to be recorded and SaveChangesAsync called
            _txRepo.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Status == TransactionStatus.Failed &&
                t.Type == TransactionType.TransferOut &&
                t.IdempotencyKey == req.IdempotencyKey)), Times.Once);

            _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleTransferAsync_RecordsFailedTransaction_OnInvalidAmount()
        {
            // Arrange
            var from = new Account("From", 1000m);
            var to = new Account("To", 100m);

            // Mock repository calls
            _accountRepo.SetupSequence(r => r.GetByIdAsync(from.Id))
                .ReturnsAsync(from) // first call to get the account for withdrawal
                .ReturnsAsync(from); // second call in catch block to record failed transaction

            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            // Create request with invalid amount (zero));
            var req = MakeRequest(from.Id, to.Id, 1_000_000m); // triggers insufficient funds

            // Act
            var res = await _handler.HandleTransferAsync(req);

            // Assert - Result should indicate failure
            Assert.False(res.IsSuccess);
            Assert.Contains("Insufficient funds for withdrawal", res.ErrorMessage);

            // Verify a failed transaction was created for the source account
            _txRepo.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Status == TransactionStatus.Failed &&
                t.Type == TransactionType.TransferOut &&
                t.IdempotencyKey == req.IdempotencyKey &&
                t.Amount == req.Amount
            )), Times.Once);

            // Verify the unit of work saved the failed transaction
            _uow.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleTransferAsync_ReturnsFailure_OnUnexpectedException_AndRollsBack()
        {
            // Arrange: cause unexpected exception when adding transactions
            var from = new Account("From", 1000m);
            var to = new Account("To", 100m);

            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            _txRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<Transaction>>()))
                  .ThrowsAsync(new Exception("db addrange failure"));

            var req = MakeRequest(from.Id, to.Id, 100m);

            // Act
            var res = await _handler.HandleTransferAsync(req);

            // Assert
            Assert.False(res.IsSuccess);
            Assert.Contains("An error occurred", res.ErrorMessage);

            _uow.Verify(u => u.RollbackAsync(), Times.Once);
        }
    }
}