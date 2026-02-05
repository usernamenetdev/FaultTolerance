using Graduation.ServiceDefaults.Metrics;
using OrderService.Contracts;
using OrderService.Data;
using OrderService.Infrastructure;
using Polly.CircuitBreaker;

namespace OrderService.Domain
{
    public class OrderApplicationService
    {
        private readonly OrderDbContext _db;
        private readonly PaymentClient _paymentClient;
        private readonly IResilienceMetrics _metrics;

        public OrderApplicationService(
            OrderDbContext db,
            PaymentClient paymentClient,
            IResilienceMetrics metrics)
        {
            _db = db;
            _paymentClient = paymentClient;
            _metrics = metrics;
        }

        public async Task<Order> CreateOrderAsync(string userId, decimal amount, string currency, string fingerprint, CancellationToken ct)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = amount,
                Currency = currency.Trim().ToUpperInvariant(),
                Fingerprint = fingerprint.Trim(),
                PaymentStatus = PaymentStatus.Unknown
            };
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            try
            {
                var payReq = new PaymentRequest
                {
                    OrderId = order.Id,
                    UserId = order.UserId,
                    Amount = order.Amount,
                    Currency = order.Currency,
                    Fingerprint = order.Fingerprint
                };

                var payResp = await _paymentClient.ProcessPaymentAsync(payReq, ct);

                if (payResp.Status == "Completed")
                {
                    order.PaymentStatus = PaymentStatus.Completed;
                    order.PaymentId = payResp.PaymentId;

                    var outbox = new OutboxMessage
                    {
                        Id = Guid.NewGuid(),
                        OutboxType = OutboxType.Receipt,
                        UserId = order.UserId.ToString()
                    };
                    _db.OutboxMessages.Add(outbox);
                    _metrics.OutboxEnqueued();
                }
                else
                {
                    order.PaymentStatus = PaymentStatus.Failed;
                    order.FailureReason = payResp.Error;
                }
            }
            catch (BrokenCircuitException)
            {
                _metrics.CircuitBreakerShortCircuit("paymentservice");
                order.PaymentStatus = PaymentStatus.Failed;
                order.FailureReason = "PaymentService недоступен.";
            }
            catch (Exception ex)
            {
                order.PaymentStatus = PaymentStatus.Failed;
                order.FailureReason = $"Ошибка при обработке платежа: {ex.Message}";
            }

            await _db.SaveChangesAsync();

            return order;
        }
    }
}
