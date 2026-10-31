using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>Hecho: una entrega pasó a tener un repartidor responsable.</summary>
public sealed record CourierAssigned(
    DeliveryId DeliveryId,
    OrderId OrderId,
    CourierId CourierId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Hecho: se quitó al repartidor de una entrega y esta volvió a estar pendiente.</summary>
public sealed record CourierReleased(
    DeliveryId DeliveryId,
    OrderId OrderId,
    CourierId CourierId,
    ReleaseReason Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
