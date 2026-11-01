namespace LogiTracker.Application.Notifications;

/// <summary>
/// Puerto hacia el servicio externo de notificaciones (contrato C-04).
/// El adaptador traduce el hecho a SMS, correo o push y debe ser idempotente por <see cref="NotificationRequest.MessageId"/>.
/// </summary>
public interface INotificationSender
{
    /// <summary>
    /// Lanza <see cref="NotificationTransientException"/> si el fallo puede resolverse reintentando,
    /// y <see cref="NotificationPermanentException"/> si reintentar no servirá (por ejemplo, formato rechazado).
    /// </summary>
    Task SendAsync(NotificationRequest request, CancellationToken cancellationToken);
}

public sealed record NotificationRequest(Guid MessageId, string EventType, string Payload);

public sealed class NotificationTransientException : Exception
{
    public NotificationTransientException() { }
    public NotificationTransientException(string message) : base(message) { }
    public NotificationTransientException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class NotificationPermanentException : Exception
{
    public NotificationPermanentException() { }
    public NotificationPermanentException(string message) : base(message) { }
    public NotificationPermanentException(string message, Exception innerException) : base(message, innerException) { }
}
