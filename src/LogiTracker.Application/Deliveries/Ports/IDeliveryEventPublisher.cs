using LogiTracker.Domain.Common;

namespace LogiTracker.Application.Deliveries.Ports;

/// <summary>
/// Puerto de publicación de hechos del dominio (contratos C-03 y C-04).
/// Registra los hechos para su envío; el envío real ocurre después de confirmar la transacción (ADR-003).
/// </summary>
public interface IDeliveryEventPublisher
{
    Task PublishAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken);
}
