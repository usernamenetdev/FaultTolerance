namespace OrderService.Domain
{
    public class Order
    {
        public Guid Id { get; set; }
        public string? UserId { get; set; }

        public decimal Amount { get; set; }
        public string Currency { get; set; } = "RUB";
        public string Fingerprint { get; set; } = default!;

        public Guid? PaymentId { get; set; }        // ID платежа (присваивается после успешного PaymentService)
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unknown; // статус платежа (синхронизируется с PaymentService)
        public string? FailureReason { get; set; }  // причина неудачи платежа (если PaymentStatus = Failed)

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}
