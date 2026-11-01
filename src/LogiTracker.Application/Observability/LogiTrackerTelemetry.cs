using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace LogiTracker.Application.Observability;

/// <summary>
/// Señales de observabilidad del flujo (docs/actividad-4/riesgos-y-observabilidad.md).
/// Solo se etiquetan identificadores técnicos y códigos de resultado; nunca direcciones, nombres ni claves.
/// Usa las APIs estándar de .NET, de modo que la aplicación no depende de un proveedor de observabilidad concreto.
/// </summary>
public static class LogiTrackerTelemetry
{
    public const string SourceName = "LogiTracker.Deliveries";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    private static readonly Counter<long> Assignments = Meter.CreateCounter<long>(
        "logitracker.assignments",
        unit: "{assignment}",
        description: "Intentos de asignación de repartidor por resultado (assigned, rejected) y código de error.");

    private static readonly Histogram<double> AssignmentDuration = Meter.CreateHistogram<double>(
        "logitracker.assignment.duration",
        unit: "ms",
        description: "Duración del caso de uso de asignación.");

    private static readonly Counter<long> OutboxDispatched = Meter.CreateCounter<long>(
        "logitracker.outbox.dispatched",
        unit: "{message}",
        description: "Avisos entregados al servicio de notificaciones.");

    private static readonly Counter<long> OutboxFailedAttempts = Meter.CreateCounter<long>(
        "logitracker.outbox.failed_attempts",
        unit: "{attempt}",
        description: "Intentos de envío fallidos que se volverán a intentar.");

    private static readonly Counter<long> OutboxDeadLettered = Meter.CreateCounter<long>(
        "logitracker.outbox.dead_lettered",
        unit: "{message}",
        description: "Avisos apartados como fallidos (DLQ) que requieren revisión.");

    public static void RecordAssignment(string outcome, string? errorCode, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "outcome", outcome },
            { "error_code", errorCode ?? "none" },
        };
        Assignments.Add(1, tags);
        AssignmentDuration.Record(elapsedMilliseconds, new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static void RecordOutboxDispatched(string eventType) =>
        OutboxDispatched.Add(1, new KeyValuePair<string, object?>("event_type", eventType));

    public static void RecordOutboxFailedAttempt(string eventType) =>
        OutboxFailedAttempts.Add(1, new KeyValuePair<string, object?>("event_type", eventType));

    public static void RecordOutboxDeadLettered(string eventType) =>
        OutboxDeadLettered.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
}
