using Graduation.ServiceDefaults.Metrics;
using OrderService.Contracts;
using Polly.CircuitBreaker;

namespace OrderService.BackgroundServices
{
    public class OutboxDispatcherHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IResilienceMetrics _metrics;
        private readonly ILogger<OutboxDispatcherHostedService> _logger;

        public OutboxDispatcherHostedService(IServiceScopeFactory scopeFactory,
                                             IHttpClientFactory httpClientFactory,
                                             IResilienceMetrics metrics,
                                             ILogger<OutboxDispatcherHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _httpClientFactory = httpClientFactory;
            _metrics = metrics;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Запуск бесконечного цикла обработки Outbox
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

                    // Выбираем все необработанные сообщения
                    var pendingMessages = await db.OutboxMessages
                        .Where(msg => !msg.Processed)
                        .ToListAsync(stoppingToken);

                    if (pendingMessages.Any())
                    {
                        var client = _httpClientFactory.CreateClient("NotificationClient");
                        foreach (var msg in pendingMessages)
                        {
                            // Формируем запрос к NotificationService
                            var notification = new DeliverRequest
                            {
                                OrderId = msg.OrderId,
                                PaymentId = msg.PaymentId,
                                UserId = msg.UserId,
                                Total = msg.Total,
                                Currency = msg.Currency
                            };
                            HttpResponseMessage response;
                            try
                            {
                                response = await client.PostAsJsonAsync("/notifications/receipt", notification, stoppingToken);
                            }
                            catch (BrokenCircuitException)
                            {
                                _metrics.CircuitBreakerShortCircuit("notificationservice");
                                _logger.LogWarning("NotificationService circuit open - skipping send for now");
                                // Не помечаем сообщение обработанным, попробуем позже
                                continue;
                            }
                            catch (HttpRequestException ex)
                            {
                                _logger.LogWarning(ex, "Failed to send notification for Payment {PaymentId}", msg.PaymentId);
                                // Оставляем в pending для повторной попытки
                                continue;
                            }

                            if (response.IsSuccessStatusCode)
                            {
                                // Успешно отправлено
                                msg.Processed = true;
                                msg.ProcessedAtUtc = DateTime.UtcNow;
                                _metrics.OutboxDispatched();  // декрементируем счётчик pending
                                _logger.LogInformation("Outbox message {MessageId} for Payment {PaymentId} sent successfully",
                                                       msg.Id, msg.PaymentId);
                            }
                            else
                            {
                                // Сервер вернул ошибку (напр., 500) – можно залогировать и оставить сообщение для повтора
                                _logger.LogWarning("NotificationService returned status {StatusCode} for Payment {PaymentId}",
                                                   response.StatusCode, msg.PaymentId);
                            }
                        }
                        // Сохраняем изменения (пометка Processed) вне цикла, одним коммитом
                        await db.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in OutboxDispatcher");
                    // В случае непредвиденной ошибки просто продолжаем цикл
                }

                // Задержка перед следующей проверкой (например, 1 секунда)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }

}
