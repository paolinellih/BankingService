using BankingService.Application.DTOs;
using BankingService.Application.Interfaces.Handlers;
using BankingService.Application.Results;
using BankingService.Application.Services.Handlers.Accounts;
using BankingService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingService.Tests.Application.Handlers
{
    public class GetAccountSummaryHandlerTests
    {
        private readonly Mock<ILogger<GetAccountSummaryHandler>> _loggerMock;
        private readonly Mock<IGetAccountTransactionsHandler> _handlerMock;
        
        private readonly GetAccountSummaryHandler _handler;
        
        public GetAccountSummaryHandlerTests()
        {
            _loggerMock = new Mock<ILogger<GetAccountSummaryHandler>>();
            _handlerMock = new Mock<IGetAccountTransactionsHandler>();
            
            _handler = new GetAccountSummaryHandler(_loggerMock.Object, _handlerMock.Object);
        }
        
        [Fact]
        public async Task HandleAsync_CallsGetAccountTransactionsHandler()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            
            var transactionList = new List<TransactionDto>
            {
                new TransactionDto(
                    Id: Guid.NewGuid(), 
                    AccountId: accountId,
                    Type: TransactionType.Deposit.ToString(), 
                    Status: "Posted",
                    Amount: 1000m,
                    BalanceAfter: 1000m,
                    Timestamp: DateTimeOffset.UtcNow.AddHours(-3),
                    PostedAt: DateTimeOffset.UtcNow.AddHours(-3),
                    Description: null,
                    IdempotencyKey: null
                ),
                new TransactionDto(
                    Id: Guid.NewGuid(), 
                    AccountId: accountId,
                    Type: TransactionType.Withdrawal.ToString(), 
                    Status: "Posted",
                    Amount: 200m,
                    BalanceAfter: 800m,
                    Timestamp: DateTimeOffset.UtcNow.AddHours(-2),
                    PostedAt: DateTimeOffset.UtcNow.AddHours(-2),
                    Description: null,
                    IdempotencyKey: null
                )
            };
            
            _handlerMock
                .Setup(h => h.HandleAsync(accountId))
                .ReturnsAsync(Result<IEnumerable<TransactionDto>>.Success(transactionList));
            
            // Act
            var result = await _handler.HandleAsync(accountId);

            var summary = result.Value!;
            
            // Assert
            Assert.True(result.IsSuccess);
            Assert.Equal(accountId, summary.AccountId);
            Assert.Equal(800m, summary.Balance);
            Assert.Equal(1000m, summary.TotalDeposits);
            Assert.Equal(200m, summary.Withdrawals);
            Assert.Equal(TransactionType.Withdrawal.ToString(), summary.LastActivity);
            Assert.Equal(transactionList.Last().PostedAt, summary.LastActivityDate);
            _handlerMock.Verify(h => h.HandleAsync(accountId), Times.Once);
        }
        
        [Fact]
        public async Task HandleAsync_WhenNoTransactions_ReturnsEmptySummary()
        {
            // Arrange
            var accountId = Guid.NewGuid();

            _handlerMock
                .Setup(x => x.HandleAsync(accountId))
                .ReturnsAsync(Result<IEnumerable<TransactionDto>>.Success(new List<TransactionDto>()));

            // Act
            var result = await _handler.HandleAsync(accountId);

            // Assert
            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Value);

            var summary = result.Value;

            Assert.Equal(accountId, summary.AccountId);
            Assert.Equal(0m, summary.Balance);
            Assert.Equal(0m, summary.TotalDeposits);
            Assert.Equal(0m, summary.Withdrawals);
            Assert.Equal("No Activity", summary.LastActivity);
            Assert.Equal(DateTimeOffset.MinValue, summary.LastActivityDate);
        }
        
        [Fact]
        public async Task HandleAsync_WhenTransactionHandlerFails_ReturnsFailure()
        {
            // Arrange
            var accountId = Guid.NewGuid();

            _handlerMock
                .Setup(x => x.HandleAsync(accountId))
                .ReturnsAsync(Result<IEnumerable<TransactionDto>>.Failure("Repo error"));

            // Act
            var result = await _handler.HandleAsync(accountId);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("Repo error", result.ErrorMessage);
        }
        
        [Fact]
        public async Task HandleAsync_WhenExceptionThrown_ReturnsFailure()
        {
            // Arrange
            var accountId = Guid.NewGuid();

            _handlerMock
                .Setup(x => x.HandleAsync(accountId))
                .ThrowsAsync(new InvalidOperationException("Boom"));

            // Act
            var result = await _handler.HandleAsync(accountId);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Contains("Boom", result.ErrorMessage);
        }
    }
}