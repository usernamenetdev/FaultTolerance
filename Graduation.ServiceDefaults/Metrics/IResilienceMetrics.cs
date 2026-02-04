namespace Graduation.ServiceDefaults.Metrics;

public enum IdempotencyResult
{
    Miss,
    Hit,
    InProgress,
    Conflict
}

public interface IResilienceMetrics
{
    void CircuitBreakerShortCircuit(string dependency);

    void RecordIdempotencyResult(string operation, IdempotencyResult result);

    void OutboxEnqueued();
    void OutboxDispatched();
}
