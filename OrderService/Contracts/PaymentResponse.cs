namespace OrderService.Contracts
{
    public class PaymentResponse
    {
        public string? Status { get; set; }
        public Guid PaymentId { get; set; }
        public string? Error { get; set; }
    }
}
