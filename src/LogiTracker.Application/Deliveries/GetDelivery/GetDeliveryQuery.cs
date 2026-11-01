using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;

namespace LogiTracker.Application.Deliveries.GetDelivery;

public sealed record AssignmentDetails(
    Guid CourierId,
    Guid AssignedBy,
    DateTimeOffset AssignedAt,
    Guid? ReleasedBy,
    DateTimeOffset? ReleasedAt,
    string? ReleaseReason);

public sealed record DeliveryDetails(
    Guid Id,
    Guid OrderId,
    string Status,
    string Street,
    string City,
    string PostalCode,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int PackageCount,
    Guid? ActiveCourierId,
    IReadOnlyList<AssignmentDetails> History);

/// <summary>Consulta el estado, el repartidor activo y el historial de asignaciones de una entrega.</summary>
public sealed class GetDeliveryQuery
{
    private readonly IDeliveryRepository _deliveries;

    public GetDeliveryQuery(IDeliveryRepository deliveries) => _deliveries = deliveries;

    public async Task<DeliveryDetails?> ExecuteAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        DeliveryId id;
        try
        {
            id = new DeliveryId(deliveryId);
        }
        catch (DomainRuleViolationException)
        {
            return null;
        }

        var delivery = await _deliveries.GetAsync(id, cancellationToken);
        if (delivery is null)
            return null;

        return new DeliveryDetails(
            delivery.Id.Value,
            delivery.OrderId.Value,
            delivery.Status.ToString(),
            delivery.Address.Street,
            delivery.Address.City,
            delivery.Address.PostalCode,
            delivery.Window.Start,
            delivery.Window.End,
            delivery.PackageCount,
            delivery.ActiveAssignment?.CourierId.Value,
            delivery.History
                .Select(a => new AssignmentDetails(
                    a.CourierId.Value,
                    a.AssignedBy.Value,
                    a.AssignedAt,
                    a.ReleasedBy?.Value,
                    a.ReleasedAt,
                    a.ReleaseReason?.Value))
                .ToList());
    }
}
