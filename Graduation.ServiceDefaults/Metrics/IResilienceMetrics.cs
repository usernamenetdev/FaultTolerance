namespace Graduation.ServiceDefaults.Metrics;

public enum IdempotencyResult
{
    Miss,
    Hit,
    InProgress,
    Conflict
}

public enum OutboxDispatchResult
{
    Sent = 1,
    Failed = 2
}

public interface IResilienceMetrics
{
    void CircuitBreakerShortCircuit(string dependency);

    void RecordIdempotencyResult(string operation, IdempotencyResult result);

    void OutboxEnqueued();
    void OutboxDispatched(); // вызывать и при Sent, и при Failed

    // Рекомендую, чтобы после рестарта gauge стал корректным
    void OutboxPendingSync(long pending);

    // Опционально: чтобы различать sent/failed
    void OutboxDispatchResult(OutboxDispatchResult result);
}