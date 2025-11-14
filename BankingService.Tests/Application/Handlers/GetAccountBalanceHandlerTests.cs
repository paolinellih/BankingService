using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Domain.Entities;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Handlers
{
    public class GetAccountBalanceHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<ILogger<GetAccountBalanceHandler>> _loggerMock = new();
        private readonly GetAccountBalanceHandler _handler;

        public GetAccountBalanceHandlerTests()
        {
            _handler = new GetAccountBalanceHandler(
                _loggerMock.Object,
                _accountRepoMock.Object);
        }

        [Fact]
        public async Task HandleAsync_ReturnsAccount_WhenAccountExists()
        {
            var account = new Account("John Doe", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id)).ReturnsAsync(account);

            var result = await _handler.HandleAsync(account.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);
            Assert.Equal(account.Id, result.Value!.Id);
            Assert.Equal(account.Balance, result.Value.Balance);
        }

        [Fact]
        public async Task HandleAsync_ReturnsFailure_WhenAccountNotFound()
        {
            var accountId = Guid.NewGuid();
            _accountRepoMock.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync((Account?)null);

            var result = await _handler.HandleAsync(accountId);

            Assert.False(result.IsSuccess);
            Assert.Contains("Account not found", result.ErrorMessage);
        }

        [Fact]
        public async Task HandleAsync_ReturnsFailure_WhenExceptionOccurs()
        {
            var accountId = Guid.NewGuid();
            _accountRepoMock.Setup(r => r.GetByIdAsync(accountId)).ThrowsAsync(new Exception("DB error"));

            var result = await _handler.HandleAsync(accountId);

            Assert.False(result.IsSuccess);
            Assert.Contains("Failed to retrieve balance: DB error", result.ErrorMessage);
        }
    }
}