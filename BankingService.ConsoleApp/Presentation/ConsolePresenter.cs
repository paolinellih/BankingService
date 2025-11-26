using System.Globalization;
using BankingService.Application.DTOs;

namespace BankingService.ConsoleApp.Presentation;

public sealed class ConsolePresenter : IConsolePresenter
{
    public void ShowTitle(string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n=== {title} ===\n");
        Console.ResetColor();
    }

    public void ShowMessage(string message, ConsoleColor? color = null)
    {
        if (color.HasValue)
            Console.ForegroundColor = color.Value;
        
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public void ShowAccountInfo(string label, string accountNumber, decimal balance, string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"  {label}: ");
        Console.ResetColor();
        Console.WriteLine($"{accountNumber} - Balance: {balance.ToString("C", culture)}");
    }

    public void ShowTransactionHistory(IEnumerable<TransactionDto> transactions, string timeZoneId, string cultureName, string tzAbbr)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var culture = new CultureInfo(cultureName);

        Console.WriteLine($"{"Time (" + tzAbbr + ")",-25} {"Status",-10} {"Type",-15} {"Amount",15} {"Balance",15}");
        Console.WriteLine(new string('-', 80));

        foreach (var transaction in transactions)
        {
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(transaction.Timestamp.UtcDateTime, timeZone);
            var timeStr = $"{localTime:yyyy-MM-dd HH:mm:ss}";
            var amountStr = transaction.Amount.ToString("C", culture);
            var balanceStr = transaction.BalanceAfter.ToString("C", culture);

            var statusColor = transaction.Status switch
            {
                "Posted" => ConsoleColor.Green,
                "Failed" => ConsoleColor.Red,
                "Pending" => ConsoleColor.Yellow,
                "Reversed" => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            Console.Write($"{timeStr,-25} ");
            Console.ForegroundColor = statusColor;
            Console.Write($"{transaction.Status,-10}");
            Console.ResetColor();
            Console.WriteLine($" {transaction.Type,-15} {amountStr,15} {balanceStr,15}");

            if (!string.IsNullOrEmpty(transaction.Description) &&
                (transaction.Status == "Failed" ||
                 transaction.Status == "Reversed" ||
                 transaction.Description.StartsWith("Reversal")))
            {
                var descColor = transaction.Status switch
                {
                    "Failed" => ConsoleColor.DarkRed,
                    "Reversed" => ConsoleColor.DarkMagenta,
                    _ => ConsoleColor.DarkCyan
                };

                Console.ForegroundColor = descColor;
                Console.WriteLine($"{"",25}   |_ {transaction.Description}");
                Console.ResetColor();
            }
        }
    }
    public void ShowError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    public void ShowSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }
    
    public string FormatCurrency(decimal amount, string culture)
        => amount.ToString("C", new CultureInfo(culture));

    public void ShowAccountSummary(AccountSummaryDto summary, string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        Console.WriteLine("\nAccount Summary:");
        Console.WriteLine($"  Account ID       : {summary.AccountId}");
        Console.WriteLine($"  Balance          : {summary.Balance.ToString("C", culture)}");
        Console.WriteLine($"  Total Deposits   : {summary.TotalDeposits.ToString("C", culture)}");
        Console.WriteLine($"  Total Withdrawals: {summary.Withdrawals.ToString("C", culture)}");
        Console.WriteLine($"  Last Activity    : {summary.LastActivity} on {summary.LastActivityDate.ToString("yyyy-MM-dd HH:mm:ss", culture)}\n");
    }
}
