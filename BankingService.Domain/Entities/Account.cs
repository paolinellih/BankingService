using BankingService.Domain.Aggregates;
using BankingService.Domain.Events;
using BankingService.Domain.Events.Account;

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
    public string CountryName { get; private set; }
    public string TimeZone { get; private set; }
    public string Culture { get; private set; }
    public string Abbreviation { get; private set; }

    private Account() {}

    public Account(
        string accountHolderName,
        decimal initialDeposit,
        string currency = "USD",
        decimal dailyWithdrawalLimit = 1000m,
        string countryName = "USA",
        string timeZone = "Eastern Standard Time",
        string culture = "en-US",
        string abbreviation = "USD")
    {
        
        if(string.IsNullOrWhiteSpace(accountHolderName))
            throw new ArgumentException("Account holder name cannot be empty.", nameof(accountHolderName));
    
        if (initialDeposit < 0)
            throw new ArgumentException("Initial deposit cannot be negative.", nameof(initialDeposit));
    
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        Id = Guid.NewGuid();
        AccountNumber = GenerateAccountNumber();
        AccountHolderName = accountHolderName;
        Balance = initialDeposit;
        Currency = currency;
        DailyWithdrawalLimit = dailyWithdrawalLimit;
        CountryName = countryName;
        TimeZone = timeZone;
        Culture = culture;
        Abbreviation = abbreviation;
        CreatedAt = DateTimeOffset.UtcNow;
        LastModifiedAt = CreatedAt;
        IsActive = true;
        
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