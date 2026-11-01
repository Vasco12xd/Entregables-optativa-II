using Microsoft.Extensions.Logging;

namespace LogiTracker.Infrastructure.Observability;

internal static partial class InfrastructureLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Simulated notification {MessageId} ({EventType}) delivered to courier and customer channels")]
    public static partial void NotificationSent(ILogger logger, Guid messageId, string eventType);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "Duplicate notification {MessageId} ignored (idempotent consumer)")]
    public static partial void NotificationDuplicateIgnored(ILogger logger, Guid messageId);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error,
        Message = "Outbox dispatcher cycle failed")]
    public static partial void DispatcherCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information,
        Message = "Database migrations applied")]
    public static partial void MigrationsApplied(ILogger logger);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information,
        Message = "Demo data seeded: {CourierCount} couriers and {OperatorCount} operators")]
    public static partial void DemoDataSeeded(ILogger logger, int courierCount, int operatorCount);
}
