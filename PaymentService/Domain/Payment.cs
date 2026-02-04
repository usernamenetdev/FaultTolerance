namespace PaymentService.Domain;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";

    public string Fingerprint { get; set; } = default!;

    public PaymentStatus Status { get; set; }
    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
