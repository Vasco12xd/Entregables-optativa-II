namespace LogiTracker.Application.Outbox;

public enum OutboxStatus
{
    /// <summary>Pendiente de envío (o esperando el próximo reintento).</summary>
    Pending,
    /// <summary>Tomado por un despachador; si su lease vence sin terminar, vuelve a estar disponible.</summary>
    Processing,
    /// <summary>Entregado al servicio de notificaciones.</summary>
    Processed,
    /// <summary>Apartado tras agotar los reintentos o por un error permanente (DLQ). Requiere revisión.</summary>
    DeadLettered,
}

/// <summary>
/// Hecho del dominio guardado en la misma transacción que la entrega, pendiente de enviarse (patrón outbox).
/// El contenido solo lleva identificadores y códigos: nunca la dirección ni datos personales del cliente.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
