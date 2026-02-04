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
    private static readonly UpDownCounter<long> OutboxPendingCount =
        Meter.CreateUpDownCounter<long>(
            name: "outbox_pending_count",
            unit: "{message}",
            description: "Текущее количество сообщений в outbox, ожидающих отправки");

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

    public void OutboxEnqueued() => OutboxPendingCount.Add(1);

    public void OutboxDispatched() => OutboxPendingCount.Add(-1);
}