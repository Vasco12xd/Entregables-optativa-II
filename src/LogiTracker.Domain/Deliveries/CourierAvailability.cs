using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>
/// Fotografía de la disponibilidad de un repartidor en un momento dado.
/// La regla de "ruta saturada" (término T-02) vive solo aquí: si el negocio cambia el criterio,
/// solo cambia este objeto (escenario de calidad EC-07).
/// </summary>
public sealed record CourierAvailability
{
    public CourierAvailability(bool isOnShift, int assignedDeliveries, int maxDeliveries)
    {
        if (maxDeliveries <= 0 || assignedDeliveries < 0)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidCourierAvailability);

        IsOnShift = isOnShift;
        AssignedDeliveries = assignedDeliveries;
        MaxDeliveries = maxDeliveries;
    }

    public bool IsOnShift { get; }
    public int AssignedDeliveries { get; }
    public int MaxDeliveries { get; }

    /// <summary>INV-04: el repartidor ya lleva tantas entregas activas como su capacidad máxima.</summary>
    public bool IsAtCapacity => AssignedDeliveries >= MaxDeliveries;
}
