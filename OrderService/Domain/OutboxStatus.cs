namespace OrderService.Domain
{
    public enum OutboxStatus : byte
    {
        Pending = 0,
        Sent = 1,
        Failed = 2
    }
}
