namespace LogiTracker.Infrastructure.Resilience;

public enum NotificationFaultMode
{
    None,
    /// <summary>El proveedor no responde: se reintenta con espera creciente.</summary>
    Transient,
    /// <summary>El proveedor rechaza el mensaje: se aparta de inmediato como fallido.</summary>
    Permanent,
}

/// <summary>
/// Interruptores para provocar fallas controladas en desarrollo y comprobar la respuesta del sistema (video 28).
/// Nunca se exponen fuera del entorno de desarrollo.
/// </summary>
public sealed class FaultInjectionState
{
    public bool AvailabilityDown { get; set; }
    public NotificationFaultMode NotificationFault { get; set; }
}
