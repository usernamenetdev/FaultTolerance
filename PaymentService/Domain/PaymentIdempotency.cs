namespace PaymentService.Domain;

public sealed class PaymentIdempotency
{
    public Guid IdempotencyKey { get; set; } = default!;
    public string RequestHash { get; set; } = default!;

    public Guid? PaymentId { get; set; }
    public IdempotencyStatus Status { get; set; }

    public PaymentStatus? ResultStatus { get; set; }
    public string? ResultError { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
