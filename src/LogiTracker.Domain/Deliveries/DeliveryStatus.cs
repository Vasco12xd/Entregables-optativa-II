namespace LogiTracker.Domain.Deliveries;

/// <summary>Estados de la entrega (docs/actividad-3/modelo-dominio.md).</summary>
public enum DeliveryStatus
{
    PendingAssignment,
    Assigned,
    InTransit,
    Delivered,
    Failed,
    Cancelled,
}
