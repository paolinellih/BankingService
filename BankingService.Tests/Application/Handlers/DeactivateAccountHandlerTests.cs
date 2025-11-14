using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Domain.Entities;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BankingService.Tests.Application.Handlers
{
    public class DeactivateAccountHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ILogger<DeactivateAccountHandler>> _loggerMock = new();
        private readonly DeactivateAccountHandler _handler;

        public DeactivateAccountHandlerTests()
        {
            _handler = new DeactivateAccountHandler(
                _loggerMock.Object,
                _accountRepoMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task HandleAsync_Succeeds_WhenAccountExists()
        {
            var account = new Account("John Doe", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id)).ReturnsAsync(account);
            _accountRepoMock.Setup(r => r.UpdateAsync(account)).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _handler.HandleAsync(account.Id);

            Assert.True(result.IsSuccess);
            Assert.False(account.IsActive);

            _accountRepoMock.Verify(r => r.UpdateAsync(account), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_Fails_WhenAccountNotFound()
        {
            var accountId = Guid.NewGuid();
            _accountRepoMock.Setup(r => r.GetByIdAsync(accountId)).ReturnsAsync((Account?)null);

            var result = await _handler.HandleAsync(accountId);

            Assert.False(result.IsSuccess);
            Assert.Contains("Account not found", result.ErrorMessage);

            _accountRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Account>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ThrowsException_PropagatesError()
        {
            var account = new Account("John Doe", 1000m);
            _accountRepoMock.Setup(r => r.GetByIdAsync(account.Id)).ReturnsAsync(account);
            _accountRepoMock.Setup(r => r.UpdateAsync(account)).ThrowsAsync(new Exception("DB error"));

            var ex = await Assert.ThrowsAsync<Exception>(() => _handler.HandleAsync(account.Id));

            Assert.Equal("DB error", ex.Message);
        }
    }
}