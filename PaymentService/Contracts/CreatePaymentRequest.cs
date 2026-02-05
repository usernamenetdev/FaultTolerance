namespace PaymentService.Contracts
{
    public sealed record CreatePaymentRequest(
         Guid OrderId,
         string UserId,
         decimal Amount,
         string Currency,
         string Fingerprint
     );
}
