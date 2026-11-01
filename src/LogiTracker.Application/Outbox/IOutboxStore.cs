namespace LogiTracker.Application.Outbox;

/// <summary>Puerto de acceso a la bandeja de salida.</summary>
public interface IOutboxStore
{
    /// <summary>
    /// Toma hasta <paramref name="batchSize"/> mensajes listos para enviarse y los marca en proceso durante <paramref name="lease"/>.
    /// Varias instancias pueden despachar a la vez sin tomar el mismo mensaje.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken);

    Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken);

    Task ScheduleRetryAsync(Guid messageId, int attempts, DateTimeOffset nextAttemptAt, string lastError, CancellationToken cancellationToken);

    Task MarkDeadLetteredAsync(Guid messageId, int attempts, string lastError, CancellationToken cancellationToken);

    Task<IReadOnlyList<OutboxMessage>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Devuelve un mensaje apartado a la cola para un nuevo intento controlado. false si no estaba apartado.</summary>
    Task<bool> RequeueAsync(Guid messageId, DateTimeOffset now, CancellationToken cancellationToken);
}
