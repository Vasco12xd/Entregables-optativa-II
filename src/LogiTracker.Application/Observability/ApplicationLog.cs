using Microsoft.Extensions.Logging;

namespace LogiTracker.Application.Observability;

/// <summary>
/// Mensajes de registro estructurados del flujo. Solo incluyen identificadores y códigos:
/// la dirección del cliente, los nombres y las claves de acceso nunca se registran.
/// </summary>
internal static partial class ApplicationLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Courier {CourierId} assigned to delivery {DeliveryId} by operator {OperatorId}")]
    public static partial void CourierAssigned(ILogger logger, Guid deliveryId, Guid courierId, Guid operatorId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "Assignment of courier {CourierId} to delivery {DeliveryId} was already in place; nothing changed")]
    public static partial void AssignmentAlreadyInPlace(ILogger logger, Guid deliveryId, Guid courierId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Assignment of courier {CourierId} to delivery {DeliveryId} rejected with code {ErrorCode}")]
    public static partial void AssignmentRejected(ILogger logger, Guid deliveryId, Guid courierId, string errorCode);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error,
        Message = "Courier availability source unavailable while assigning delivery {DeliveryId}")]
    public static partial void AvailabilityUnavailable(ILogger logger, Guid deliveryId, Exception exception);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information,
        Message = "Courier released from delivery {DeliveryId} by operator {OperatorId}")]
    public static partial void CourierReleased(ILogger logger, Guid deliveryId, Guid operatorId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning,
        Message = "Release of delivery {DeliveryId} rejected with code {ErrorCode}")]
    public static partial void ReleaseRejected(ILogger logger, Guid deliveryId, string errorCode);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information,
        Message = "Delivery {DeliveryId} created for order {OrderId}")]
    public static partial void DeliveryCreated(ILogger logger, Guid deliveryId, Guid orderId);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information,
        Message = "Delivery for order {OrderId} already existed ({DeliveryId}); request treated as a retry")]
    public static partial void DeliveryAlreadyExisted(ILogger logger, Guid deliveryId, Guid orderId);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information,
        Message = "Outbox message {MessageId} ({EventType}) dispatched")]
    public static partial void OutboxDispatched(ILogger logger, Guid messageId, string eventType);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Warning,
        Message = "Outbox message {MessageId} ({EventType}) failed on attempt {Attempt}; retrying at {NextAttemptAt}")]
    public static partial void OutboxRetryScheduled(ILogger logger, Guid messageId, string eventType, int attempt, DateTimeOffset nextAttemptAt, Exception exception);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error,
        Message = "Outbox message {MessageId} ({EventType}) moved to dead letters after {Attempt} attempts")]
    public static partial void OutboxDeadLettered(ILogger logger, Guid messageId, string eventType, int attempt, Exception exception);
}
