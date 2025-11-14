using BankingService.Domain.Enums;

namespace BankingService.Domain.Entities;

public class IdempotentRequest
{
    public Guid Id { get; private set; }
    public string IdempotencyKey { get; private set; }
    public string OperationType { get; private set; }
    public string RequestData { get; private set; }
    public string? ResponseData { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public IdempotencyStatus Status { get; private set; }
    
    private IdempotentRequest(){}
    
    public IdempotentRequest(string idempotencyKey, string operationType, string requestData)
    {
        Id = Guid.NewGuid();
        IdempotencyKey = idempotencyKey ?? throw new ArgumentNullException(nameof(idempotencyKey));
        OperationType = operationType ?? throw new ArgumentNullException(nameof(operationType));
        RequestData = requestData ?? throw new ArgumentNullException(nameof(requestData));
        CreatedAt = DateTimeOffset.UtcNow;
        Status = IdempotencyStatus.InProgress;
    }
    
    public void MarkCompleted(string responseData)
    {
        ResponseData = responseData ?? throw new ArgumentNullException(nameof(responseData));
        Status = IdempotencyStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed()
    {
        Status = IdempotencyStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}