namespace PaymentService.Contracts
{
    public sealed record PaymentStatusResponse(
        Guid PaymentId,
        Guid OrderId,
        string? UserId,
        decimal Amount,
        string Currency,
        string Fingerprint,
        string Status,
        string? FailureReason,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        DateTime? CompletedAtUtc
    );
}
