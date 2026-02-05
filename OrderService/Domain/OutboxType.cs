namespace OrderService.Domain
{
        public enum OutboxType : byte
        {
            Magiclink = 0,
            Receipt = 1
        }
}
