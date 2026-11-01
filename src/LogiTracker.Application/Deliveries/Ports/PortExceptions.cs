namespace LogiTracker.Application.Deliveries.Ports;

/// <summary>Otra operación modificó la entrega desde que se leyó (control optimista, ADR-002 D-04).</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException() { }
    public ConcurrencyConflictException(string message) : base(message) { }
    public ConcurrencyConflictException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>La fuente de disponibilidad de repartidores no respondió (escenario ESC-09).</summary>
public sealed class AvailabilityUnavailableException : Exception
{
    public AvailabilityUnavailableException() { }
    public AvailabilityUnavailableException(string message) : base(message) { }
    public AvailabilityUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// El repartidor alcanzó su capacidad entre la consulta de disponibilidad y el guardado.
/// La lanza la guarda atómica del repositorio que cierra la limitación del ADR-002 (ver ADR-003).
/// </summary>
public sealed class CourierCapacityExceededException : Exception
{
    public CourierCapacityExceededException() { }
    public CourierCapacityExceededException(string message) : base(message) { }
    public CourierCapacityExceededException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Ya existe una entrega para el pedido indicado.</summary>
public sealed class DuplicateOrderException : Exception
{
    public DuplicateOrderException() { }
    public DuplicateOrderException(string message) : base(message) { }
    public DuplicateOrderException(string message, Exception innerException) : base(message, innerException) { }
}
