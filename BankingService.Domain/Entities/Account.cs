using BankingService.Domain.Aggregates;
using BankingService.Domain.Enums;
using BankingService.Domain.Events;
using BankingService.Domain.Events.Account;
using BankingService.Domain.ValueObjects;

namespace BankingService.Domain.Entities;

public class Account : AggregateRoot
{
    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; }
    public string AccountHolderName { get; private set; }
    public decimal Balance { get; private set; }
    public string Currency { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastModifiedAt { get; private set; }
    public bool IsActive { get; private set; }
    public int Version { get; private set; }
    public decimal DailyWithdrawalLimit { get; private set; }
    public decimal TodayWithdrawalAmount { get; private set; }
    public AccountLocale Locale { get; private set; }
    public AccountType AccountType { get; private set; }

    private Account() {}

    public Account(
        string accountHolderName,
        decimal initialDeposit,
        string currency = "USD",
        decimal dailyWithdrawalLimit = 1000m,
        AccountLocale? locale = null,
        AccountType? accountType = AccountType.Standard)
    {
        
        if(string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Account holder name cannot be empty.", nameof(accountHolderName));
    
        if (initialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative.", nameof(initialDeposit));
    
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));
        
        if (dailyWithdrawalLimit < 0)
            throw new ArgumentException("Daily withdrawal limit cannot be negative.", nameof(dailyWithdrawalLimit));
        
        Locale = locale ?? new AccountLocale("USA", "Eastern Standard Time", "en-US", "EST"); // default locale

        Id = Guid.NewGuid();
        AccountNumber = GenerateAccountNumber();
        AccountHolderName = accountHolderName;
        Balance = initialDeposit;
        Currency = currency;
        DailyWithdrawalLimit = dailyWithdrawalLimit;
        CreatedAt = DateTimeOffset.UtcNow;
        LastModifiedAt = CreatedAt;
        IsActive = true;
        AccountType = accountType ?? AccountType.Standard;
        
        AddDomainEvent(new AccountCreated(Id, AccountNumber, accountHolderName, initialDeposit, currency));
    }
    
    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Deposit amount must be positive.", nameof(amount));
        if(!IsActive)
            throw new InvalidOperationException("Cannot deposit to an inactive account.");

        Balance += amount;
        Version++;
        LastModifiedAt = DateTimeOffset.UtcNow;
        
        AddDomainEvent(new AccountDeposited(Id, amount, Balance, Currency));
    }
    
    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Withdrawal amount must be positive.", nameof(amount));
        if(!IsActive)
            throw new InvalidOperationException("Cannot withdraw from an inactive account.");
        if (amount > Balance)
            throw new InvalidOperationException("Insufficient funds for withdrawal.");
        
        // Daily limit check
        var today = DateTimeOffset.UtcNow.Date;
        if (LastModifiedAt.Date != today)
            TodayWithdrawalAmount = 0; // reset daily tracker

        if (TodayWithdrawalAmount + amount > DailyWithdrawalLimit)
            throw new InvalidOperationException(
                $"Withdrawal would exceed daily limit of {DailyWithdrawalLimit:C}."
            );

        Balance -= amount;
        TodayWithdrawalAmount += amount;
        Version++;
        LastModifiedAt = DateTimeOffset.UtcNow;
        
        AddDomainEvent(new AccountWithdrawn(Id, amount, Balance, Currency));
    }
    
    public void Deactivate()
    {
        IsActive = false;
        LastModifiedAt = DateTimeOffset.UtcNow;
        
        AddDomainEvent(new AccountDeactivated(Id, LastModifiedAt));
    }
    
    private string GenerateAccountNumber()
    {
        return $"ACCT- {DateTimeOffset.UtcNow:yyyyMMddHHmmss} {Random.Shared.Next(1000, 9999)}";
    }
}