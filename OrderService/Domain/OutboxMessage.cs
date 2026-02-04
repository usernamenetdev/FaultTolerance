namespace OrderService.Domain
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }

        public OutboxType OutboxType { get; set; }

        public string UserId { get; set; } = default!;

        public int Attempts { get; set; }

        public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
        public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
