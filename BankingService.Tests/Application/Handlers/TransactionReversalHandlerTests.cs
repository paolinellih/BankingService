using BankingService.Application.Services.Handlers.Transactions;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Handlers
{
    public class TransactionReversalHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepo = new();
        private readonly Mock<ITransactionRepository> _txRepo = new();
        private readonly Mock<IUnitOfWork> _uow = new();
        private readonly Mock<IAccountLockManager> _lockManager = new();
        private readonly Mock<ILogger<TransactionReversalHandler>> _logger = new();
        private readonly TransactionReversalHandler _handler;

        public TransactionReversalHandlerTests()
        {
            _lockManager
                .Setup(l => l.LockAccountAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new TestLock());
            _lockManager
                .Setup(l => l.LockAccountsAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new TestLock());

            _handler = new TransactionReversalHandler(
                _accountRepo.Object,
                _txRepo.Object,
                _uow.Object,
                _lockManager.Object,
                _logger.Object
            );
        }

        private class TestLock : IDisposable { public void Dispose() { } }

        private Account MakeAccount(decimal balance = 1000m, string currency = "USD") =>
            new("Test", balance, currency);

        private Transaction MakeTransaction(
            Guid accountId,
            TransactionType type,
            decimal amount = 100m,
            string currency = "USD",
            Guid? related = null,
            string key = "KEY")
        {
            var tx = new Transaction(accountId, type, amount, 1000m, "desc", related, key);
            tx.MarkAsPosted();
            return tx;
        }

        [Fact]
        public async Task HandleReversalAsync_Fails_WhenTransactionNotFound()
        {
            var txId = Guid.NewGuid();
            _txRepo.Setup(r => r.GetByIdAsync(txId)).ReturnsAsync((Transaction?)null);

            var result = await _handler.HandleReversalAsync(txId, "reason");

            Assert.False(result.IsSuccess);
            Assert.Contains("Transaction not found", result.ErrorMessage);
        }

        [Fact]
        public async Task HandleReversalAsync_Fails_WhenTransactionAlreadyReversed()
        {
            var tx = MakeTransaction(Guid.NewGuid(), TransactionType.Deposit);
            // Simulate reversal already done by using paired transaction scenario
            tx.MarkAsReversed();
            _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);

            var result = await _handler.HandleReversalAsync(tx.Id, "reason");

            Assert.False(result.IsSuccess);
            Assert.Contains("posted transactions can be reversed", result.ErrorMessage);
        }

        [Fact]
        public async Task ReverseDeposit_Succeeds_WhenSufficientBalance()
        {
            var acc = MakeAccount(500);
            var tx = MakeTransaction(acc.Id, TransactionType.Deposit, 200);
            _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
            _accountRepo.Setup(r => r.GetByIdAsync(acc.Id)).ReturnsAsync(acc);

            var result = await _handler.HandleReversalAsync(tx.Id, "reason");

            Assert.True(result.IsSuccess);
            _accountRepo.Verify(r => r.UpdateAsync(acc), Times.Once);
            _txRepo.Verify(r => r.UpdateAsync(tx), Times.Once);
            _txRepo.Verify(r => r.AddAsync(It.Is<Transaction>(t => t.Type == TransactionType.Withdrawal && t.Amount == 200)), Times.Once);
            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ReverseDeposit_Fails_WhenInsufficientBalance()
        {
            var acc = MakeAccount(100); // Less than deposit amount
            var tx = MakeTransaction(acc.Id, TransactionType.Deposit, 200);
            _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
            _accountRepo.Setup(r => r.GetByIdAsync(acc.Id)).ReturnsAsync(acc);

            var result = await _handler.HandleReversalAsync(tx.Id, "reason");

            Assert.False(result.IsSuccess);
            Assert.Contains("Insufficient balance", result.ErrorMessage);
            _uow.Verify(u => u.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task ReverseWithdrawal_Succeeds()
        {
            var acc = MakeAccount(500);
            var tx = MakeTransaction(acc.Id, TransactionType.Withdrawal, 200);
            _txRepo.Setup(r => r.GetByIdAsync(tx.Id)).ReturnsAsync(tx);
            _accountRepo.Setup(r => r.GetByIdAsync(acc.Id)).ReturnsAsync(acc);

            var result = await _handler.HandleReversalAsync(tx.Id, "reason");

            Assert.True(result.IsSuccess);
            _txRepo.Verify(r => r.AddAsync(It.Is<Transaction>(t => t.Type == TransactionType.Deposit && t.Amount == 200)), Times.Once);
            _accountRepo.Verify(r => r.UpdateAsync(acc), Times.Once);
            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ReverseTransfer_Succeeds()
        {
            var from = MakeAccount(1000);
            var to = MakeAccount(500);
            var txOut = MakeTransaction(from.Id, TransactionType.TransferOut, 200, related: to.Id);
            var txIn = MakeTransaction(to.Id, TransactionType.TransferIn, 200, related: from.Id, key: txOut.IdempotencyKey);

            _txRepo.Setup(r => r.GetByIdAsync(txOut.Id)).ReturnsAsync(txOut);
            _txRepo.Setup(r => r.GetByAccountIdAsync(to.Id)).ReturnsAsync(new[] { txIn });
            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            var result = await _handler.HandleReversalAsync(txOut.Id, "reason");

            Assert.True(result.IsSuccess);
            _txRepo.Verify(r => r.AddAsync(It.Is<Transaction>(t => t.Type == TransactionType.TransferOut)), Times.Once);
            _txRepo.Verify(r => r.AddAsync(It.Is<Transaction>(t => t.Type == TransactionType.TransferIn)), Times.Once);
            _txRepo.Verify(r => r.UpdateAsync(txOut), Times.Once);
            _txRepo.Verify(r => r.UpdateAsync(txIn), Times.Once);
            _accountRepo.Verify(r => r.UpdateAsync(from), Times.Once);
            _accountRepo.Verify(r => r.UpdateAsync(to), Times.Once);
            _uow.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task ReverseTransfer_Fails_WhenRecipientInsufficientBalance()
        {
            var from = MakeAccount();
            var to = MakeAccount(100); // Less than transfer
            var txOut = MakeTransaction(from.Id, TransactionType.TransferOut, 200, related: to.Id);
            var txIn = MakeTransaction(to.Id, TransactionType.TransferIn, 200, related: from.Id, key: txOut.IdempotencyKey);

            _txRepo.Setup(r => r.GetByIdAsync(txOut.Id)).ReturnsAsync(txOut);
            _txRepo.Setup(r => r.GetByAccountIdAsync(to.Id)).ReturnsAsync(new[] { txIn });
            _accountRepo.Setup(r => r.GetByIdAsync(from.Id)).ReturnsAsync(from);
            _accountRepo.Setup(r => r.GetByIdAsync(to.Id)).ReturnsAsync(to);

            var result = await _handler.HandleReversalAsync(txOut.Id, "reason");

            Assert.False(result.IsSuccess);
            Assert.Contains("Insufficient balance", result.ErrorMessage);
            _uow.Verify(u => u.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleReversalAsync_CatchesException()
        {
            var txId = Guid.NewGuid();
            _txRepo.Setup(r => r.GetByIdAsync(txId)).ThrowsAsync(new Exception("boom"));

            var result = await _handler.HandleReversalAsync(txId, "reason");

            Assert.False(result.IsSuccess);
            Assert.Contains("Error reversing transaction", result.ErrorMessage);
        }
    }
}