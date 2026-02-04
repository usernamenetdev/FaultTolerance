namespace PaymentService.Domain;

public enum IdempotencyStatus : byte
{
    InProgress = 0,
    Completed = 1
}
