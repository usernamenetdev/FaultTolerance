using System.Diagnostics.Metrics;

namespace Graduation.ServiceDefaults.Metrics;

public sealed class ResilienceMetrics : IResilienceMetrics
{
    public const string MeterName = "service.resilience";
    private const string MeterVersion = "1.0.0";

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    // Circuit Breaker: circuit_breaker_short_circuit_total {dependency=?}
    private static readonly Counter<long> ShortCircuitTotal =
        Meter.CreateCounter<long>(
            name: "circuit_breaker_short_circuit_total",
            unit: "{call}",
            description: "Количество прерванных размыкателем цепи вызовов");

    // Idempotency: idempotency_result_total {operation=?, result=miss|hit|in_progress|conflict}
    private static readonly Counter<long> IdempotencyResultTotal =
        Meter.CreateCounter<long>(
            name: "idempotency_result_total",
            unit: "{request}",
            description: "Результаты обработки Idempotency-Key: miss/hit/in_progress/conflict");

    // Outbox: outbox_pending_count (UpDown)
    // --- Outbox: backlog (Gauge) + activity (Counters) ---
    private static long _outboxPending;

    private static readonly ObservableGauge<long> OutboxPendingGauge =
        Meter.CreateObservableGauge<long>(
            name: "outbox_pending_count",
            unit: "{message}",
            observeValue: () => Interlocked.Read(ref _outboxPending),
            description: "Текущее количество сообщений в outbox, ожидающих отправки");

    private static readonly Counter<long> OutboxEnqueuedTotal =
        Meter.CreateCounter<long>(
            name: "outbox_enqueued_total",
            unit: "{message}",
            description: "Сколько outbox-сообщений создано");

    private static readonly Counter<long> OutboxDispatchedTotal =
        Meter.CreateCounter<long>(
            name: "outbox_dispatched_total",
            unit: "{message}",
            description: "Сколько outbox-сообщений закрыто (Sent или Failed)");

    private static readonly Counter<long> OutboxDispatchResultTotal =
        Meter.CreateCounter<long>(
            name: "outbox_dispatch_result_total",
            unit: "{message}",
            description: "Результат обработки outbox-сообщений: sent/failed");

    public void CircuitBreakerShortCircuit(string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency))
            dependency = "unknown";

        ShortCircuitTotal.Add(1,
            new KeyValuePair<string, object?>("dependency", dependency));
    }

    public void RecordIdempotencyResult(string operation, IdempotencyResult result)
    {
        if (string.IsNullOrWhiteSpace(operation))
            operation = "unknown";

        var resultTag = result switch
        {
            IdempotencyResult.Miss => "miss",
            IdempotencyResult.Hit => "hit",
            IdempotencyResult.InProgress => "in_progress",
            IdempotencyResult.Conflict => "conflict",
            _ => "unknown"
        };

        IdempotencyResultTotal.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("result", resultTag));
    }

    // Вызывать ПОСЛЕ успешного SaveChanges() при создании outbox
    public void OutboxEnqueued()
    {
        OutboxEnqueuedTotal.Add(1);
        Interlocked.Increment(ref _outboxPending);
    }

    // Вызывать при переводе сообщения в Sent ИЛИ Failed (т.е. оно больше не Pending)
    public void OutboxDispatched()
    {
        OutboxDispatchedTotal.Add(1);
        Interlocked.Decrement(ref _outboxPending);
    }

    public void OutboxPendingSync(long pending) =>
        Interlocked.Exchange(ref _outboxPending, pending);

    public void OutboxDispatchResult(OutboxDispatchResult result)
    {
        var tag = result == Metrics.OutboxDispatchResult.Sent ? "sent" : "failed";
        OutboxDispatchResultTotal.Add(1, new KeyValuePair<string, object?>("result", tag));
    }
}