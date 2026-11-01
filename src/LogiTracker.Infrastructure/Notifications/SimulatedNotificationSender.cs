using System.Collections.Concurrent;
using LogiTracker.Application.Notifications;
using LogiTracker.Infrastructure.Observability;
using LogiTracker.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;

namespace LogiTracker.Infrastructure.Notifications;

/// <summary>
/// Adaptador simulado del servicio externo de notificaciones. El caso del curso no ofrece un proveedor real,
/// así que este adaptador registra el envío y permite provocar fallas temporales o permanentes para comprobar
/// los reintentos y la DLQ. Reemplazarlo por un proveedor real (SMS, correo) solo cambia este archivo.
/// Es idempotente por identificador de mensaje: un mensaje repetido no genera un segundo aviso.
/// </summary>
internal sealed class SimulatedNotificationSender : INotificationSender
{
    private readonly ConcurrentDictionary<Guid, byte> _delivered = new();
    private readonly FaultInjectionState _faults;
    private readonly ILogger<SimulatedNotificationSender> _logger;

    public SimulatedNotificationSender(FaultInjectionState faults, ILogger<SimulatedNotificationSender> logger)
    {
        _faults = faults;
        _logger = logger;
    }

    public Task SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (_faults.NotificationFault)
        {
            case NotificationFaultMode.Transient:
                throw new NotificationTransientException("Simulated: notification provider did not respond.");
            case NotificationFaultMode.Permanent:
                throw new NotificationPermanentException("Simulated: notification provider rejected the message format.");
        }

        if (!_delivered.TryAdd(request.MessageId, 0))
        {
            InfrastructureLog.NotificationDuplicateIgnored(_logger, request.MessageId);
            return Task.CompletedTask;
        }

        InfrastructureLog.NotificationSent(_logger, request.MessageId, request.EventType);
        return Task.CompletedTask;
    }
}
