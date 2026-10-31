using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>
/// Raíz del agregado Entrega. Es el único punto por el que se puede asignar o liberar un repartidor,
/// y protege las invariantes INV-01 a INV-10 (docs/actividad-3/invariantes-y-escenarios.md).
/// No depende de bases de datos, HTTP ni frameworks.
/// </summary>
public sealed class Delivery
{
    private readonly List<CourierAssignment> _history = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private Delivery()
    {
        // Constructor para la reconstrucción desde persistencia.
    }

    private Delivery(
        DeliveryId id,
        OrderId orderId,
        DeliveryAddress address,
        DeliveryWindow window,
        int packageCount,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrderId = orderId;
        Address = address;
        Window = window;
        PackageCount = packageCount;
        CreatedAt = createdAt;
        Status = DeliveryStatus.PendingAssignment;
    }

    public DeliveryId Id { get; private set; } = null!;
    public OrderId OrderId { get; private set; } = null!;
    public DeliveryAddress Address { get; private set; } = null!;
    public DeliveryWindow Window { get; private set; } = null!;
    public int PackageCount { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Todas las asignaciones que tuvo la entrega, incluidas las liberadas (trazabilidad, EC-10).</summary>
    public IReadOnlyList<CourierAssignment> History => _history;

    /// <summary>La asignación vigente. INV-01: como máximo una.</summary>
    public CourierAssignment? ActiveAssignment => _history.LastOrDefault(a => a.IsActive);

    /// <summary>Hechos ocurridos y aún no publicados.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>Crea la entrega a partir del contrato C-01 "Pedido listo para logística".</summary>
    public static Delivery CreateFromOrder(
        DeliveryId id,
        OrderId orderId,
        DeliveryAddress address,
        DeliveryWindow window,
        int packageCount,
        DateTimeOffset now)
    {
        if (packageCount <= 0)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidPackageCount);

        return new Delivery(id, orderId, address, window, packageCount, now);
    }

    /// <summary>
    /// Indica por qué no se puede asignar este repartidor, o null si se puede.
    /// El caso de uso la usa para no consultar la disponibilidad en vano (paso 2 del flujo).
    /// </summary>
    public string? GetAssignmentRejection(CourierId courierId)
    {
        if (Status == DeliveryStatus.Assigned && ActiveAssignment!.CourierId == courierId)
            return null;                                         // Reintento idempotente (INV-10)
        if (Status == DeliveryStatus.Assigned)
            return DeliveryErrors.DeliveryAlreadyAssigned;       // INV-01
        if (Status != DeliveryStatus.PendingAssignment)
            return DeliveryErrors.DeliveryNotAssignable;         // INV-02, INV-09
        return null;
    }

    public void AssignCourier(
        CourierId courierId,
        CourierAvailability availability,
        OperatorId assignedBy,
        DateTimeOffset now)
    {
        var rejection = GetAssignmentRejection(courierId);
        if (rejection is not null)
            throw new DomainRuleViolationException(rejection);

        if (Status == DeliveryStatus.Assigned)
            return;                                              // Mismo repartidor: no se duplica (INV-10)

        if (!availability.IsOnShift)
            throw new DomainRuleViolationException(DeliveryErrors.CourierUnavailable);       // INV-03
        if (availability.IsAtCapacity)
            throw new DomainRuleViolationException(DeliveryErrors.CourierCapacityReached);   // INV-04

        _history.Add(new CourierAssignment(courierId, assignedBy, now));           // INV-05
        Status = DeliveryStatus.Assigned;                                          // INV-06
        _domainEvents.Add(new CourierAssigned(Id, OrderId, courierId, now));
    }

    public void ReleaseCourier(ReleaseReason reason, OperatorId releasedBy, DateTimeOffset now)
    {
        if (Status != DeliveryStatus.Assigned)
            throw new DomainRuleViolationException(DeliveryErrors.DeliveryNotReleasable);    // INV-09

        var active = ActiveAssignment!;
        active.Release(reason, releasedBy, now);                                   // INV-07
        Status = DeliveryStatus.PendingAssignment;                                 // INV-06
        _domainEvents.Add(new CourierReleased(Id, OrderId, active.CourierId, reason, now));
    }

    /// <summary>El repartidor recogió el paquete. Fuera del flujo diseñado; se incluye para el ciclo de estados.</summary>
    public void MarkInTransit()
    {
        if (Status != DeliveryStatus.Assigned)
            throw new DomainRuleViolationException(DeliveryErrors.DeliveryNotAssignable);

        Status = DeliveryStatus.InTransit;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
