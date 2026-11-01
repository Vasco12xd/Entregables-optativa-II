using LogiTracker.Domain.Deliveries;

namespace LogiTracker.Application.Deliveries.Ports;

/// <summary>Puerto de persistencia de entregas.</summary>
public interface IDeliveryRepository
{
    Task<Delivery?> GetAsync(DeliveryId id, CancellationToken cancellationToken);

    Task<Delivery?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken);

    /// <summary>Guarda una entrega nueva. Lanza <see cref="DuplicateOrderException"/> si ya existe una para el pedido.</summary>
    Task AddAsync(Delivery delivery, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda los cambios de la entrega y los hechos registrados en la bandeja de salida, en una sola transacción.
    /// Lanza <see cref="ConcurrencyConflictException"/> si otra operación la modificó desde que se leyó (INV-01)
    /// y <see cref="CourierCapacityExceededException"/> si el repartidor se saturó entre la consulta y el guardado (INV-04).
    /// </summary>
    Task SaveAsync(Delivery delivery, CancellationToken cancellationToken);
}
