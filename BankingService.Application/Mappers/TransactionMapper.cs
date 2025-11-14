using BankingService.Application.DTOs;
using BankingService.Domain.Entities;

namespace BankingService.Application.Mappers;

public static class TransactionMapper
{
    public static TransactionDto ToDto(Transaction transaction)
    {
        return new TransactionDto(
            transaction.Id,
            transaction.AccountId,
            transaction.Type.ToString(),
            transaction.Status.ToString(),
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.Timestamp,
            transaction.PostedAt,
            transaction.Description,
            transaction.IdempotencyKey
        );
    }
}