using BankingService.Domain.Aggregates;
using BankingService.Domain.Enums;
using BankingService.Domain.Events.Transaction;

namespace BankingService.Domain.Entities;

public class Transaction : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public string? Description { get; private set; }
    public Guid? RelatedAccountId { get; private set; }
    public string? IdempotencyKey { get; private set; }

    private Transaction() {}

    public Transaction(
        Guid accountId,
        TransactionType type,
        decimal amount,
        decimal balanceAfter,
        string? description = null,
        Guid? relatedAccountId = null,
        string? idempotencyKey = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Transaction amount must be positive.", nameof(amount));
        
        Id = Guid.NewGuid();
        AccountId = accountId;
        Type = type;
        Status = TransactionStatus.Pending; // Start as pending
        Amount = amount;
        BalanceAfter = balanceAfter;
        Timestamp = DateTime.UtcNow;
        Description = description;
        RelatedAccountId = relatedAccountId;
        IdempotencyKey = idempotencyKey;
    }
    
    public void MarkAsPosted()
    {
        if (Status != TransactionStatus.Pending)
            throw new InvalidOperationException($"Cannot post transaction in {Status} status.");
        
        Status = TransactionStatus.Posted;
        PostedAt = DateTime.UtcNow;
        
        AddDomainEvent(new TransactionPosted(Id, AccountId, Type, Amount, BalanceAfter, PostedAt.Value));
    }

    public void MarkAsFailed()
    {
        if (Status == TransactionStatus.Posted)
            throw new InvalidOperationException("Cannot fail a posted transaction.");
        
        Status = TransactionStatus.Failed;
        
        AddDomainEvent(new TransactionFailed(Id, AccountId, Type, Amount, Description ?? "Transaction failed."));
    }

    public void MarkAsReversed()
    {
        if (Status != TransactionStatus.Posted)
            throw new InvalidOperationException("Can only reverse posted transactions.");
        
        Status = TransactionStatus.Reversed;
        
        AddDomainEvent(new TransactionReversed(Id, AccountId, Type, Amount, BalanceAfter));
    }
}