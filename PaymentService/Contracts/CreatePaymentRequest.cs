namespace PaymentService.Contracts
{
    public sealed record CreatePaymentRequest(
         Guid OrderId,
         Guid UserId,
         decimal Amount,
         string Currency,
         string Fingerprint
     );
}
