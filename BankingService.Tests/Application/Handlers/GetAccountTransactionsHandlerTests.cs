using BankingService.Application.DTOs;
using BankingService.Application.Services.Handlers.Transactions;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BankingService.Tests.Application.Handlers
{
    public class GetAccountTransactionsHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<ITransactionRepository> _txRepoMock = new();
        private readonly Mock<ILogger<GetAccountTransactionsHandler>> _loggerMock = new();
        private readonly GetAccountTransactionsHandler _handler;

        public GetAccountTransactionsHandlerTests()
        {
            _handler = new GetAccountTransactionsHandler(
                _loggerMock.Object,
                _accountRepoMock.Object,
                _txRepoMock.Object
            );
        }

        private Transaction MakeTransaction(Guid accountId, decimal amount = 100m,
            TransactionType type = TransactionType.Deposit)
            => new(accountId, type, amount, 1000m, "desc");

        [Fact]
        public async Task HandleAsync_ReturnsFailure_WhenAccountNotFound()
        {
            var accountId = Guid.NewGuid();
            _accountRepoMock.Setup(r => r.GetByIdAsync(accountId))
                .ReturnsAsync((BankingService.Domain.Entities.Account?)null);

            var result = await _handler.HandleAsync(accountId);

            Assert.False(result.IsSuccess);
            Assert.Contains("Account not found", result.ErrorMessage);
        }

        [Fact]
        public async Task HandleAsync_ReturnsEmptyList_WhenNoTransactions()
        {
            var account = new BankingService.Domain.Entities.Account("Test", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id)).ReturnsAsync(account);
            _txRepoMock.Setup(r => r.GetByAccountIdAsync(account.Id)).ReturnsAsync(Array.Empty<Transaction>());

            var result = await _handler.HandleAsync(account.Id);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value!);
        }

        [Fact]
        public async Task HandleAsync_ReturnsTransactions_InCorrectOrder()
        {
            var account = new BankingService.Domain.Entities.Account("Test", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id)).ReturnsAsync(account);

            var tx1 = MakeTransaction(account.Id, 100);
            var tx2 = MakeTransaction(account.Id, 200);
            
            _txRepoMock.Setup(r => r.GetByAccountIdAsync(account.Id))
                .ReturnsAsync(new[] { tx2, tx1 }); // return newest first

            var result = await _handler.HandleAsync(account.Id);

            Assert.True(result.IsSuccess);
            var list = result.Value!.ToList();
            Assert.Equal(2, list.Count);
            Assert.Equal(tx2.Amount, list[0].Amount); // tx2 first (newest)
            Assert.Equal(tx1.Amount, list[1].Amount); // tx1 second (older)
        }

        [Fact]
        public async Task HandleAsync_ReturnsFailure_WhenExceptionThrown()
        {
            var accountId = Guid.NewGuid();
            _accountRepoMock.Setup(r => r.GetByIdAsync(accountId)).ThrowsAsync(new Exception("boom"));

            var result = await _handler.HandleAsync(accountId);

            Assert.False(result.IsSuccess);
            Assert.Contains("An error occurred while retrieving transactions", result.ErrorMessage);
        }
    }
}
