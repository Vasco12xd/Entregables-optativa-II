using LogiTracker.Application.Outbox;
using LogiTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LogiTracker.Infrastructure.Outbox;

/// <summary>Adaptador de <see cref="IOutboxStore"/> sobre PostgreSQL.</summary>
internal sealed class EfOutboxStore : IOutboxStore
{
    private readonly LogiTrackerDbContext _db;

    public EfOutboxStore(LogiTrackerDbContext db) => _db = db;

    /// <summary>
    /// FOR UPDATE SKIP LOCKED permite que varias instancias despachen en paralelo sin tomar el mismo mensaje.
    /// Un mensaje "Processing" cuyo lease venció (instancia caída) vuelve a estar disponible: entrega al menos una vez,
    /// por eso el adaptador de notificaciones es idempotente por identificador de mensaje.
    /// </summary>
    public async Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(
        int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken)
    {
        var nowUtc = now.ToUniversalTime();
        var leaseUntil = nowUtc + lease;

        return await _db.OutboxMessages
            .FromSql($"""
                UPDATE deliveries.outbox_messages AS m
                SET status = 'Processing', locked_until = {leaseUntil}
                WHERE m.id IN (
                    SELECT id FROM deliveries.outbox_messages
                    WHERE (status = 'Pending' AND next_attempt_at <= {nowUtc})
                       OR (status = 'Processing' AND locked_until < {nowUtc})
                    ORDER BY occurred_at
                    LIMIT {batchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING m.*
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken) =>
        _db.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.Processed)
                .SetProperty(m => m.ProcessedAt, processedAt.ToUniversalTime())
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null), cancellationToken);

    public Task ScheduleRetryAsync(Guid messageId, int attempts, DateTimeOffset nextAttemptAt, string lastError, CancellationToken cancellationToken) =>
        _db.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.Pending)
                .SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.NextAttemptAt, nextAttemptAt.ToUniversalTime())
                .SetProperty(m => m.LastError, lastError)
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null), cancellationToken);

    public Task MarkDeadLetteredAsync(Guid messageId, int attempts, string lastError, CancellationToken cancellationToken) =>
        _db.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.DeadLettered)
                .SetProperty(m => m.Attempts, attempts)
                .SetProperty(m => m.LastError, lastError)
                .SetProperty(m => m.LockedUntil, (DateTimeOffset?)null), cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken) =>
        await _db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.DeadLettered)
            .OrderBy(m => m.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<bool> RequeueAsync(Guid messageId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var updated = await _db.OutboxMessages
            .Where(m => m.Id == messageId && m.Status == OutboxStatus.DeadLettered)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.Status, OutboxStatus.Pending)
                .SetProperty(m => m.Attempts, 0)
                .SetProperty(m => m.NextAttemptAt, now.ToUniversalTime()), cancellationToken);
        return updated > 0;
    }
}
