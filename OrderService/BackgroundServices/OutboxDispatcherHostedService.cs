using Graduation.ServiceDefaults.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using OrderService.Data;
using OrderService.Domain;
using OrderService.Infrastructure;
using Polly.CircuitBreaker;

namespace OrderService.Application;

public sealed class OutboxDispatcherOptions
{
    public int BatchSize { get; set; } = 50;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    public int MaxAttempts { get; set; } = 8;

    // Экспоненциальный backoff
    public TimeSpan BackoffBase { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan BackoffMax { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OutboxDispatcherOptions> _opt;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxDispatcherOptions> opt)
    {
        _scopeFactory = scopeFactory;
        _opt = opt;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        await SyncPendingGaugeAtStartup(stoppingToken);

        var timer = new PeriodicTimer(_opt.Value.PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // намеренно: чтобы HostedService не падал из-за единичной ошибки
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var notification = scope.ServiceProvider.GetRequiredService<NotificationClient>();
        var metrics = scope.ServiceProvider.GetRequiredService<IResilienceMetrics>();

        var now = DateTime.UtcNow;

        var messages = await db.OutboxMessages
            .Where(x => x.Status == OutboxStatus.Pending && x.NextAttemptUtc <= now)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(_opt.Value.BatchSize)
            .ToListAsync(ct);

        foreach (var msg in messages)
        {
            try
            {
                await SendToNotificationAsync(notification, msg, ct);

                msg.Status = OutboxStatus.Sent;

                metrics.OutboxDispatchResult(OutboxDispatchResult.Sent);
                metrics.OutboxDispatched();
            }
            catch (BrokenCircuitException)
            {
                // фиксируем short-circuit по зависимости (требование по метрикам CB)
                metrics.CircuitBreakerShortCircuit("notificationservice");

                msg.Attempts++;
                msg.NextAttemptUtc = DateTime.UtcNow.Add(ComputeBackoff(msg.Attempts));

                if (msg.Attempts >= _opt.Value.MaxAttempts)
                {
                    msg.Status = OutboxStatus.Failed;
                    metrics.OutboxDispatchResult(OutboxDispatchResult.Failed);
                    metrics.OutboxDispatched();
                }
            }
            catch
            {
                msg.Attempts++;
                msg.NextAttemptUtc = DateTime.UtcNow.Add(ComputeBackoff(msg.Attempts));

                if (msg.Attempts >= _opt.Value.MaxAttempts)
                {
                    msg.Status = OutboxStatus.Failed;
                    metrics.OutboxDispatchResult(OutboxDispatchResult.Failed);
                    metrics.OutboxDispatched();
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private static Task SendToNotificationAsync(NotificationClient client, OutboxMessage msg, CancellationToken ct) =>
        msg.OutboxType switch
        {
            OutboxType.Magiclink => client.SendMagicLinkAsync(msg.UserId, ct),
            OutboxType.Receipt => client.SendReceiptAsync(msg.UserId, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(msg.OutboxType), msg.OutboxType, "Unknown outbox type")
        };

    private TimeSpan ComputeBackoff(int attempts)
    {
        // exp backoff: base * 2^(attempts-1), capped
        var baseMs = _opt.Value.BackoffBase.TotalMilliseconds;
        var maxMs = _opt.Value.BackoffMax.TotalMilliseconds;

        var ms = baseMs * Math.Pow(2, Math.Max(0, attempts - 1));
        ms = Math.Min(ms, maxMs);

        return TimeSpan.FromMilliseconds(ms);
    }

    private async Task SyncPendingGaugeAtStartup(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var metrics = scope.ServiceProvider.GetRequiredService<IResilienceMetrics>();

        var pending = await db.OutboxMessages.CountAsync(x => x.Status == OutboxStatus.Pending, ct);
        metrics.OutboxPendingSync(pending);
    }
}