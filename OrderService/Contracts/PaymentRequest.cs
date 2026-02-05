namespace OrderService.Contracts
{
    public class PaymentRequest
    {
        public Guid OrderId { get; set; }
        public string? UserId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RUB";
        public string Fingerprint { get; set; } = default!;
    }
}
