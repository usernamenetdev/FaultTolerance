namespace OrderService.Domain
{
    public class Order
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal Total { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Unknown;
        public string? FailureReason { get; set; }  // причина неудачи (если Status = Failed)

        public Guid? PaymentId { get; set; }        // ID платежа (присваивается после успешного PaymentService)
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}
