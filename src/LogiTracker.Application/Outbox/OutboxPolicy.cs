namespace LogiTracker.Application.Outbox;

/// <summary>Política de reintentos de la bandeja de salida (video 21: reintentos limitados y espera creciente).</summary>
public sealed class OutboxPolicy
{
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan Lease { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Espera exponencial: 2 s, 4 s, 8 s... con un máximo, para no golpear un servicio caído.</summary>
    public TimeSpan RetryDelayFor(int attempt)
    {
        var factor = Math.Pow(2, Math.Max(0, attempt - 1));
        var delay = TimeSpan.FromMilliseconds(BaseRetryDelay.TotalMilliseconds * factor);
        return delay < MaxRetryDelay ? delay : MaxRetryDelay;
    }
}
