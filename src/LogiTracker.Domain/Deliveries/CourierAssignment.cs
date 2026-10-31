using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>
/// Registro de que un repartidor es (o fue) responsable de una entrega.
/// Pertenece al agregado <see cref="Delivery"/>: solo la entrega puede crearla o liberarla.
/// Ajuste de implementación (ADR-003): tiene un identificador técnico para poder persistir el historial,
/// pero sus datos no cambian salvo al liberarse.
/// </summary>
public sealed class CourierAssignment
{
    private CourierAssignment()
    {
        // Constructor para la reconstrucción desde persistencia.
    }

    internal CourierAssignment(CourierId courierId, OperatorId assignedBy, DateTimeOffset assignedAt)
    {
        Id = Guid.NewGuid();
        CourierId = courierId;
        AssignedBy = assignedBy;
        AssignedAt = assignedAt;
    }

    public Guid Id { get; private set; }
    public CourierId CourierId { get; private set; } = null!;
    public OperatorId AssignedBy { get; private set; } = null!;
    public DateTimeOffset AssignedAt { get; private set; }
    public OperatorId? ReleasedBy { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public ReleaseReason? ReleaseReason { get; private set; }

    public bool IsActive => ReleasedAt is null;

    /// <summary>INV-07: una liberación registra quién, cuándo y por qué.</summary>
    internal void Release(ReleaseReason reason, OperatorId releasedBy, DateTimeOffset releasedAt)
    {
        if (!IsActive)
            throw new DomainRuleViolationException(DeliveryErrors.DeliveryNotReleasable);

        ReleaseReason = reason;
        ReleasedBy = releasedBy;
        ReleasedAt = releasedAt;
    }
}
