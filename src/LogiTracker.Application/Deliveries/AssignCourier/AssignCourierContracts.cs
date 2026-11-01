using LogiTracker.Domain.Deliveries;

namespace LogiTracker.Application.Deliveries.AssignCourier;

/// <summary>Entrada del caso de uso: datos simples, sin tipos de HTTP ni de base de datos.</summary>
/// <param name="OperatorId">Se obtiene de la identidad autenticada, nunca del cuerpo de la petición (ADR-003).</param>
public sealed record AssignCourierCommand(Guid DeliveryId, Guid CourierId, Guid OperatorId);

/// <summary>Salida del caso de uso: éxito con los datos de la asignación, o un código del catálogo de errores.</summary>
public sealed record AssignCourierResult(
    bool Succeeded,
    string? ErrorCode,
    Guid DeliveryId,
    Guid? CourierId,
    string? Status,
    DateTimeOffset? AssignedAt)
{
    public static AssignCourierResult Ok(Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        var active = delivery.ActiveAssignment!;
        return new(true, null, delivery.Id.Value, active.CourierId.Value, delivery.Status.ToString(), active.AssignedAt);
    }

    public static AssignCourierResult Fail(Guid deliveryId, string errorCode) =>
        new(false, errorCode, deliveryId, null, null, null);
}
