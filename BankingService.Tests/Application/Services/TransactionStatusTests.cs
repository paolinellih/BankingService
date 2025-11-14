using BankingService.Domain.Entities;
using BankingService.Domain.Enums;

namespace BankingService.Tests.Application.Services;

public class TransactionStatusTests
{
    [Fact]
    public void Transaction_CreatedAsPending_ShouldHavePendingStatus()
    {
        // Arrange & Act
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Deposit,
            100m,
            1100m,
            "Test deposit");

        // Assert
        Assert.Equal(TransactionStatus.Pending, transaction.Status);
        Assert.Null(transaction.PostedAt);
    }

    [Fact]
    public void Transaction_MarkAsPosted_ShouldUpdateStatusAndTimestamp()
    {
        // Arrange
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Deposit,
            100m,
            1100m,
            "Test deposit");

        // Act
        transaction.MarkAsPosted();

        // Assert
        Assert.Equal(TransactionStatus.Posted, transaction.Status);
        Assert.NotNull(transaction.PostedAt);
        Assert.True(transaction.PostedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Transaction_MarkAsFailed_ShouldUpdateStatus()
    {
        // Arrange
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Withdrawal,
            100m,
            900m,
            "Test withdrawal");

        // Act
        transaction.MarkAsFailed();

        // Assert
        Assert.Equal(TransactionStatus.Failed, transaction.Status);
    }

    [Fact]
    public void Transaction_MarkAsReversed_OnPostedTransaction_ShouldSucceed()
    {
        // Arrange
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Deposit,
            100m,
            1100m,
            "Test deposit");
        transaction.MarkAsPosted();

        // Act
        transaction.MarkAsReversed();

        // Assert
        Assert.Equal(TransactionStatus.Reversed, transaction.Status);
    }

    [Fact]
    public void Transaction_MarkAsPosted_WhenAlreadyPosted_ShouldThrow()
    {
        // Arrange
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Deposit,
            100m,
            1100m,
            "Test deposit");
        transaction.MarkAsPosted();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => transaction.MarkAsPosted());
    }

    [Fact]
    public void Transaction_MarkAsReversed_WhenPending_ShouldThrow()
    {
        // Arrange
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Deposit,
            100m,
            1100m,
            "Test deposit");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => transaction.MarkAsReversed());
    }

    [Fact]
    public void Transaction_MarkAsFailed_WhenPosted_ShouldThrow()
    {
        // Arrange
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.Deposit,
            100m,
            1100m,
            "Test deposit");
        transaction.MarkAsPosted();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => transaction.MarkAsFailed());
    }

    [Fact]
    public void Transaction_WithIdempotencyKey_ShouldStoreKey()
    {
        // Arrange & Act
        var idempotencyKey = Guid.NewGuid().ToString();
        var transaction = new Transaction(
            Guid.NewGuid(),
            TransactionType.TransferIn,
            500m,
            1500m,
            "Transfer",
            Guid.NewGuid(),
            idempotencyKey);

        // Assert
        Assert.Equal(idempotencyKey, transaction.IdempotencyKey);
    }
}