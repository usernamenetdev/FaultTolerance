using Graduation.ServiceDefaults.Metrics;
using OrderService.Data;

namespace OrderService.Domain
{
    public class OutboxService
    {
        private readonly OrderDbContext _db;
        private readonly IResilienceMetrics _metrics;

        public OutboxService(OrderDbContext db, IResilienceMetrics metrics)
        {
            _db = db;
            _metrics = metrics;
        }

        public async Task<Guid> CreateOutboxMessageAsync(OutboxType outboxType, string userId, CancellationToken ct)
        {
            var msg = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OutboxType = outboxType,
                UserId = userId,
                Status = OutboxStatus.Pending,
                Attempts = 0,
                NextAttemptUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };

            try
            {
                _db.OutboxMessages.Add(msg);
                await _db.SaveChangesAsync(ct);
                _metrics.OutboxEnqueued();
                return msg.Id;
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
