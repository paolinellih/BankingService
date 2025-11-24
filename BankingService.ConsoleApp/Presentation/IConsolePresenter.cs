using BankingService.Application.DTOs;

namespace BankingService.ConsoleApp.Presentation;

public interface IConsolePresenter
{
    void ShowTitle(string title);
    void ShowMessage(string message, ConsoleColor? color = null);
    void ShowAccountInfo(string label, string accountNumber, decimal balance, string cultureName);
    void ShowTransactionHistory(IEnumerable<TransactionDto> transactions, string timeZoneId, string cultureName, string tzAbbr);
    void ShowError(string message);
    void ShowSuccess(string message);
    string FormatCurrency(decimal amount, string cultureName);
    void ShowAccountSummary(AccountSummaryDto summary, string cultureName);
}