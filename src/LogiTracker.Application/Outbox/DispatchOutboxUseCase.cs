using LogiTracker.Application.Common;
using LogiTracker.Application.Notifications;
using LogiTracker.Application.Observability;
using Microsoft.Extensions.Logging;

namespace LogiTracker.Application.Outbox;

public sealed record DispatchSummary(int Dispatched, int RetriesScheduled, int DeadLettered);

/// <summary>
/// Consumidor de la bandeja de salida (productor-consumidor, video 20).
/// Fallo temporal: reintento con espera creciente hasta un máximo. Fallo permanente o reintentos agotados:
/// el mensaje se aparta como fallido (Dead Letter, video 21) para revisión, sin perder la asignación ya confirmada.
/// </summary>
public sealed class DispatchOutboxUseCase
{
    private const int MaxErrorLength = 500;

    private readonly IOutboxStore _store;
    private readonly INotificationSender _sender;
    private readonly IClock _clock;
    private readonly OutboxPolicy _policy;
    private readonly ILogger<DispatchOutboxUseCase> _logger;

    public DispatchOutboxUseCase(
        IOutboxStore store,
        INotificationSender sender,
        IClock clock,
        OutboxPolicy policy,
        ILogger<DispatchOutboxUseCase> logger)
    {
        _store = store;
        _sender = sender;
        _clock = clock;
        _policy = policy;
        _logger = logger;
    }

    public async Task<DispatchSummary> ExecuteAsync(CancellationToken cancellationToken)
    {
        var messages = await _store.ClaimDueAsync(_policy.BatchSize, _clock.UtcNow, _policy.Lease, cancellationToken);
        int dispatched = 0, retries = 0, deadLettered = 0;

        foreach (var message in messages)
        {
            using var activity = LogiTrackerTelemetry.ActivitySource.StartActivity("DispatchOutboxMessage");
            activity?.SetTag("outbox.message_id", message.Id);
            activity?.SetTag("outbox.event_type", message.EventType);
            var attempt = message.Attempts + 1;

            try
            {
                await _sender.SendAsync(new NotificationRequest(message.Id, message.EventType, message.Payload), cancellationToken);
                await _store.MarkProcessedAsync(message.Id, _clock.UtcNow, cancellationToken);
                LogiTrackerTelemetry.RecordOutboxDispatched(message.EventType);
                ApplicationLog.OutboxDispatched(_logger, message.Id, message.EventType);
                dispatched++;
            }
            catch (NotificationPermanentException exception)
            {
                // Reintentar no cambiará el resultado: se aparta de inmediato.
                await DeadLetterAsync(message, attempt, exception, cancellationToken);
                deadLettered++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Fallo temporal o de causa desconocida: reintento limitado conservando el error (video 21).
                if (attempt >= _policy.MaxAttempts)
                {
                    await DeadLetterAsync(message, attempt, exception, cancellationToken);
                    deadLettered++;
                    continue;
                }

                var nextAttemptAt = _clock.UtcNow + _policy.RetryDelayFor(attempt);
                await _store.ScheduleRetryAsync(message.Id, attempt, nextAttemptAt, Describe(exception), cancellationToken);
                LogiTrackerTelemetry.RecordOutboxFailedAttempt(message.EventType);
                ApplicationLog.OutboxRetryScheduled(_logger, message.Id, message.EventType, attempt, nextAttemptAt, exception);
                retries++;
            }
        }

        return new DispatchSummary(dispatched, retries, deadLettered);
    }

    private async Task DeadLetterAsync(OutboxMessage message, int attempt, Exception exception, CancellationToken cancellationToken)
    {
        await _store.MarkDeadLetteredAsync(message.Id, attempt, Describe(exception), cancellationToken);
        LogiTrackerTelemetry.RecordOutboxDeadLettered(message.EventType);
        ApplicationLog.OutboxDeadLettered(_logger, message.Id, message.EventType, attempt, exception);
    }

    private static string Describe(Exception exception)
    {
        var text = $"{exception.GetType().Name}: {exception.Message}";
        return text.Length <= MaxErrorLength ? text : text[..MaxErrorLength];
    }
}
