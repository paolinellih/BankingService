using BankingService.Application.DTOs;
using BankingService.Application.Interfaces;
using BankingService.Application.Results;
using BankingService.ConsoleApp.Configuration;
using BankingService.ConsoleApp.Presentation;

namespace BankingService.ConsoleApp.Demo;

public sealed class InternationalBankingDemo : IBankingDemo
{
    private readonly IBankingService _bankingService;
    private readonly IConsolePresenter _console;

    private readonly CountryConfig _country1 = new("UK", "en-GB", "GMT Standard Time", "GMT");
    private readonly CountryConfig _country2 = new("USA", "en-US", "Eastern Standard Time", "EST");

    public InternationalBankingDemo(IBankingService bankingService, IConsolePresenter console)
    {
        _bankingService = bankingService;
        _console = console;
    }

    public async Task RunAsync()
    {
        _console.ShowTitle("International Banking Service Demo");

        var account1 = await CreateAccountAsync(
            $"John Smith ({_country1.Name})",
            1000m,
            _country1,
            currency: "GBP",
            dailyLimit: 2000m);

        var account2 = await CreateAccountAsync(
            $"Maria Santos ({_country2.Name})",
            50000m,
            _country2,
            currency: "USD",
            dailyLimit: 10000m);
        
        var account3 = await CreateAccountAsync(
            $"Francesca Rossi ({_country1.Name})",
            0m,
            _country1,
            currency: "USD",
            dailyLimit: 800m);


        if (account1 is null || account2 is null || account3 is null)
            return;

        await DepositAsync(account1, 200m, _country1);
        await WithdrawAsync(account2, 5000m, _country2);
        await WithdrawAsync(account2, 100_000m, _country2, expectFailure: true);
        await TransferAsync(account1, account2, 300m);
        await ReverseLastTransferAsync(account1, account2);
        await WithdrawAsync(account1, 201m, _country1, expectFailure: true);
        await WithdrawAsync(account1, 200m, _country1);
        
        // Prepare transfers
        var transfer1 = Task.Run(() => _bankingService.TransferAsync(
            new TransferRequest(account1.Id, account3.Id, 300m, Guid.NewGuid().ToString())));
        var transfer2 = Task.Run(() => _bankingService.TransferAsync(
            new TransferRequest(account2.Id, account3.Id, 500m, Guid.NewGuid().ToString())));
        // Execute concurrently
        await Task.WhenAll(transfer1, transfer2);
        
        // Wothdrawals from account3 
        await WithdrawAsync(account3, 850m, _country1, expectFailure: true);
        
        // Show final balances
        await ShowBalancesAsync(new []{account1, account2, account3});
        
        // Show transaction histories after all operations
        await ShowTransactionHistoryAsync(account1);
        await ShowTransactionHistoryAsync(account2);
        await ShowTransactionHistoryAsync(account3);
    }

    #region Account Operations

    private async Task<AccountDto?> CreateAccountAsync(
        string name,
        decimal initialDeposit,
        CountryConfig country,
        string currency = "USD",
        decimal dailyLimit = 1000m)
    {
        var request = new CreateAccountRequest(name, initialDeposit, currency, dailyLimit);

        var result = await _bankingService.CreateAccountAsync(request);

        if (!result.IsSuccess)
        {
            _console.ShowError($"Failed to create account for {name}: {result.ErrorMessage}");
            return null;
        }

        var account = result.Value!;
        _console.ShowAccountInfo(
            $"Created Account ({name})",
            account.AccountNumber,
            account.Balance,
            country.Culture);

        return account;
    }

    private async Task DepositAsync(AccountDto account, decimal amount, CountryConfig country)
    {
        _console.ShowMessage($"\nDepositing {_console.FormatCurrency(amount, country.Culture)} into {country.Name} account...");
        var result = await _bankingService.DepositAsync(CreateDepositRequest(account.Id, amount));
        ShowResult(result, account.Id, country);
    }

    private async Task WithdrawAsync(AccountDto account, decimal amount, CountryConfig country, bool expectFailure = false)
    {
        _console.ShowMessage($"\nWithdrawing {_console.FormatCurrency(amount, country.Culture)} from {country.Name} account...");
        var result = await _bankingService.WithdrawAsync(CreateWithdrawalRequest(account.Id, amount));

        if (expectFailure && !result.IsSuccess)
        {
            _console.ShowError($"Expected failure: {result.ErrorMessage}");
        }
        else
        {
            ShowResult(result, account.Id, country);
        }
    }

    private async Task TransferAsync(AccountDto from, AccountDto to, decimal amount)
    {
        _console.ShowMessage($"\nTransferring {_console.FormatCurrency(amount, _country1.Culture)} from {_country1.Name} to {_country2.Name}...");
        var result = await _bankingService.TransferAsync(new TransferRequest(from.Id, to.Id, amount, Guid.NewGuid().ToString()));

        if (!result.IsSuccess)
        {
            _console.ShowError(result.ErrorMessage!);
            return;
        }

        await ShowBalancesAsync(new []{from, to});
    }

    private async Task ReverseLastTransferAsync(AccountDto from, AccountDto to)
    {
        _console.ShowTitle("Demonstrating Chargeback (Reversal)");

        var lastTransfer = await GetLastTransferOutAsync(from);
        if (lastTransfer is null)
        {
            _console.ShowMessage("No transfers found to reverse.");
            return;
        }

        _console.ShowMessage($"Reversing transaction {lastTransfer.Id}...");
        var reversal = await _bankingService.ReverseTransactionAsync(lastTransfer.Id, "Unauthorized transaction");

        if (!reversal.IsSuccess)
        {
            _console.ShowError(reversal.ErrorMessage!);
            return;
        }

        _console.ShowMessage("Chargeback processed successfully!", ConsoleColor.Green);
        await ShowBalancesAsync(new []{from, to});
    }

    #endregion

    #region Helpers

    private static DepositRequest CreateDepositRequest(Guid accountId, decimal amount)
        => new(accountId, amount, Guid.NewGuid().ToString());

    private static WithdrawalRequest CreateWithdrawalRequest(Guid accountId, decimal amount)
        => new(accountId, amount, Guid.NewGuid().ToString());

    private async Task ShowBalancesAsync(IEnumerable<AccountDto> accounts)
    {
        foreach (var account in accounts)
        {
            var balanceResult = await _bankingService.GetAccountBalanceAsync(account.Id);
            if (!balanceResult.IsSuccess || balanceResult.Value is null)
            {
                _console.ShowError($"Failed to get balance for {account.AccountHolderName} ({account.Locale.CountryName})");
                continue;
            }

            _console.ShowMessage(
                $"{account.AccountHolderName} ({account.Locale.CountryName}) Balance: {_console.FormatCurrency(balanceResult.Value.Balance, account.Locale.Culture)}",
                ConsoleColor.Green);
        }
    }

    private void ShowResult(Result<AccountDto> result, Guid accountId, CountryConfig country)
    {
        if (result.IsSuccess)
        {
            _console.ShowMessage($"New Balance: {_console.FormatCurrency(result.Value!.Balance, country.Culture)}", ConsoleColor.Green);
        }
        else
        {
            _console.ShowError(result.ErrorMessage!);
        }
    }

    private async Task<TransactionDto?> GetLastTransferOutAsync(AccountDto account)
    {
        var txsResult = await _bankingService.GetAccountTransactionsAsync(account.Id);
        if (!txsResult.IsSuccess || txsResult.Value is null) return null;

        return txsResult.Value!
            .Where(t => t.Type == "TransferOut" && t.Status == "Posted")
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefault();
    }

    private async Task ShowTransactionHistoryAsync(AccountDto account)
    {
        var txsResult = await _bankingService.GetAccountTransactionsAsync(account.Id);
        if (!txsResult.IsSuccess || txsResult.Value is null)
        {
            _console.ShowError(txsResult.ErrorMessage!);
            return;
        }

        _console.ShowTitle($"{account.AccountHolderName} Transaction History ({account.AccountNumber})");
        
        // Reverse order
        var transactions = txsResult.Value!.OrderByDescending((t => t.Timestamp));
        
        _console.ShowTransactionHistory(transactions, account.Locale.TimeZone, account.Locale.Culture, account.Locale.Abbreviation);
    }

    #endregion
}