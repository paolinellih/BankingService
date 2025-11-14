using BankingService.Application.DTOs;
using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Domain.Entities;
using BankingService.Domain.Enums;
using BankingService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Handlers
{
    public class CreateAccountHandlerTests
    {
        private readonly Mock<IAccountRepository> _accountRepoMock = new();
        private readonly Mock<ITransactionRepository> _txRepoMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ILogger<CreateAccountHandler>> _loggerMock = new();
        private readonly CreateAccountHandler _handler;

        public CreateAccountHandlerTests()
        {
            _handler = new CreateAccountHandler(
                _loggerMock.Object,
                _accountRepoMock.Object,
                _txRepoMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        public async Task HandleAsync_CreatesAccountWithoutInitialDeposit()
        {
            var request = new CreateAccountRequest("John Doe", 0m);

            _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<Account>()))
                .ReturnsAsync((Account a) => a);

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _handler.HandleAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("John Doe", result.Value!.AccountHolderName);
            Assert.Equal(0m, result.Value.Balance);

            _accountRepoMock.Verify(r => r.AddAsync(It.IsAny<Account>()), Times.Once);
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_CreatesAccountWithInitialDeposit()
        {
            var request = new CreateAccountRequest("Jane Doe", 500m);

            _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<Account>()))
                .ReturnsAsync((Account a) => a);

            _txRepoMock.Setup(r => r.AddAsync(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) => t);

            _unitOfWorkMock.Setup(u => u.SaveChangesAsync()).Returns(Task.CompletedTask);

            var result = await _handler.HandleAsync(request);

            Assert.True(result.IsSuccess);
            Assert.Equal("Jane Doe", result.Value!.AccountHolderName);
            Assert.Equal(500m, result.Value.Balance);

            _accountRepoMock.Verify(r => r.AddAsync(It.IsAny<Account>()), Times.Once);
            _txRepoMock.Verify(r => r.AddAsync(It.Is<Transaction>(t =>
                t.Type == TransactionType.Deposit &&
                t.Amount == 500m &&
                t.Status == TransactionStatus.Posted)), Times.Once);
            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_ReturnsFailure_WhenAccountRepositoryThrows()
        {
            var request = new CreateAccountRequest("John Doe", 0m);

            _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<Account>()))
                .ThrowsAsync(new Exception("Database error"));

            var result = await _handler.HandleAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Contains("Database error", result.ErrorMessage);

            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
            _txRepoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_ReturnsFailure_WhenTransactionRepositoryThrows()
        {
            var request = new CreateAccountRequest("Jane Doe", 100m);

            _accountRepoMock.Setup(r => r.AddAsync(It.IsAny<Account>()))
                .ReturnsAsync((Account a) => a);

            _txRepoMock.Setup(r => r.AddAsync(It.IsAny<Transaction>()))
                .ThrowsAsync(new Exception("Transaction DB error"));

            var result = await _handler.HandleAsync(request);

            Assert.False(result.IsSuccess);
            Assert.Contains("Transaction DB error", result.ErrorMessage);

            _unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Never);
        }
    }
}