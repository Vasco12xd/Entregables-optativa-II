using LogiTracker.Domain.Deliveries;

namespace LogiTracker.Application.Deliveries.Ports;

/// <summary>
/// Puerto de consulta de disponibilidad (contrato C-02). Entregas define qué necesita saber;
/// la fuente real de turnos y capacidad queda en infraestructura.
/// </summary>
public interface ICourierAvailability
{
    /// <summary>
    /// Devuelve null si el repartidor no existe.
    /// Lanza <see cref="AvailabilityUnavailableException"/> si la fuente no responde.
    /// </summary>
    Task<CourierAvailability?> GetAsync(CourierId courierId, CancellationToken cancellationToken);
}
