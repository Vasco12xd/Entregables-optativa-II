using System.Text.Json;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Outbox;
using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;
using LogiTracker.Infrastructure.Persistence;

namespace LogiTracker.Infrastructure.Deliveries;

/// <summary>
/// Adaptador de <see cref="IDeliveryEventPublisher"/> con el patrón outbox (ADR-002 D-06).
/// Agrega los hechos al mismo contexto que la entrega; el repositorio los confirma en la misma transacción
/// y el despachador los envía después. Si Notificaciones falla, la asignación no se revierte.
/// </summary>
internal sealed class OutboxEventPublisher : IDeliveryEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LogiTrackerDbContext _db;

    public OutboxEventPublisher(LogiTrackerDbContext db) => _db = db;

    public Task PublishAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        foreach (var domainEvent in events)
        {
            _db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = domainEvent.GetType().Name,
                Payload = Serialize(domainEvent),
                OccurredAt = domainEvent.OccurredAt,
                Status = OutboxStatus.Pending,
                NextAttemptAt = domainEvent.OccurredAt,
            });
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Contrato publicado C-03: solo identificadores y fechas. El motivo de liberación es texto libre del operador
    /// y podría contener datos personales, por eso no viaja a sistemas externos.
    /// </summary>
    private static string Serialize(IDomainEvent domainEvent) => domainEvent switch
    {
        CourierAssigned e => JsonSerializer.Serialize(
            new { deliveryId = e.DeliveryId.Value, orderId = e.OrderId.Value, courierId = e.CourierId.Value, occurredAt = e.OccurredAt },
            JsonOptions),
        CourierReleased e => JsonSerializer.Serialize(
            new { deliveryId = e.DeliveryId.Value, orderId = e.OrderId.Value, courierId = e.CourierId.Value, occurredAt = e.OccurredAt },
            JsonOptions),
        _ => throw new NotSupportedException($"No outbox contract defined for {domainEvent.GetType().Name}."),
    };
}
