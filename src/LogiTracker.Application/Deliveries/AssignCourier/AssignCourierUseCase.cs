using System.Diagnostics;
using LogiTracker.Application.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Observability;
using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;
using Microsoft.Extensions.Logging;

namespace LogiTracker.Application.Deliveries.AssignCourier;

/// <summary>
/// Asigna un repartidor a una entrega (docs/actividad-3/casos-uso-puertos-adaptadores.md).
/// Coordina; no decide: las reglas las aplica <see cref="Delivery"/>.
/// Todas sus dependencias llegan por constructor y son interfaces.
/// </summary>
public sealed class AssignCourierUseCase
{
    private readonly IDeliveryRepository _deliveries;
    private readonly ICourierAvailability _availability;
    private readonly IDeliveryEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger<AssignCourierUseCase> _logger;

    public AssignCourierUseCase(
        IDeliveryRepository deliveries,
        ICourierAvailability availability,
        IDeliveryEventPublisher publisher,
        IClock clock,
        ILogger<AssignCourierUseCase> logger)
    {
        _deliveries = deliveries;
        _availability = availability;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AssignCourierResult> ExecuteAsync(AssignCourierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var activity = LogiTrackerTelemetry.ActivitySource.StartActivity("AssignCourier");
        activity?.SetTag("delivery.id", command.DeliveryId);
        activity?.SetTag("courier.id", command.CourierId);
        var startedAt = Stopwatch.GetTimestamp();

        var result = await ExecuteCoreAsync(command, cancellationToken);

        var outcome = result.Succeeded ? "assigned" : "rejected";
        activity?.SetTag("assignment.outcome", outcome);
        activity?.SetTag("assignment.error_code", result.ErrorCode);
        if (!result.Succeeded)
        {
            activity?.SetStatus(ActivityStatusCode.Error, result.ErrorCode);
            ApplicationLog.AssignmentRejected(_logger, command.DeliveryId, command.CourierId, result.ErrorCode!);
        }

        LogiTrackerTelemetry.RecordAssignment(outcome, result.ErrorCode, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return result;
    }

    private async Task<AssignCourierResult> ExecuteCoreAsync(AssignCourierCommand command, CancellationToken cancellationToken)
    {
        DeliveryId deliveryId;
        CourierId courierId;
        OperatorId operatorId;
        try
        {
            deliveryId = new DeliveryId(command.DeliveryId);
            courierId = new CourierId(command.CourierId);
            operatorId = new OperatorId(command.OperatorId);
        }
        catch (DomainRuleViolationException violation)
        {
            return AssignCourierResult.Fail(command.DeliveryId, violation.Code);
        }

        // 1. Cargar la entrega
        var delivery = await _deliveries.GetAsync(deliveryId, cancellationToken);

        // 2. Verificar que exista y que no tenga otro repartidor activo
        if (delivery is null)
            return AssignCourierResult.Fail(command.DeliveryId, ApplicationErrors.DeliveryNotFound);

        var rejection = delivery.GetAssignmentRejection(courierId);
        if (rejection is not null)
            return AssignCourierResult.Fail(command.DeliveryId, rejection);

        if (delivery.ActiveAssignment?.CourierId == courierId)
        {
            // Reintento idempotente (INV-10, ADR-002 D-05): ya estaba asignada a este repartidor.
            ApplicationLog.AssignmentAlreadyInPlace(_logger, command.DeliveryId, command.CourierId);
            return AssignCourierResult.Ok(delivery);
        }

        try
        {
            // 3. Consultar la disponibilidad mediante un puerto
            var availability = await _availability.GetAsync(courierId, cancellationToken);
            if (availability is null)
                return AssignCourierResult.Fail(command.DeliveryId, ApplicationErrors.CourierNotFound);

            // 4. El dominio protege las invariantes
            delivery.AssignCourier(courierId, availability, operatorId, _clock.UtcNow);

            // 5. Registrar el hecho "Repartidor asignado" en la bandeja de salida
            await _publisher.PublishAsync(delivery.DomainEvents, cancellationToken);

            // 6. Guardar la entrega y la bandeja de salida en una sola transacción
            await _deliveries.SaveAsync(delivery, cancellationToken);
            delivery.ClearDomainEvents();
        }
        catch (DomainRuleViolationException violation)
        {
            return AssignCourierResult.Fail(command.DeliveryId, violation.Code);
        }
        catch (ConcurrencyConflictException)
        {
            return AssignCourierResult.Fail(command.DeliveryId, ApplicationErrors.ConcurrencyConflict);
        }
        catch (CourierCapacityExceededException)
        {
            return AssignCourierResult.Fail(command.DeliveryId, DeliveryErrors.CourierCapacityReached);
        }
        catch (AvailabilityUnavailableException exception)
        {
            ApplicationLog.AvailabilityUnavailable(_logger, command.DeliveryId, exception);
            return AssignCourierResult.Fail(command.DeliveryId, ApplicationErrors.AvailabilityServiceUnavailable);
        }

        ApplicationLog.CourierAssigned(_logger, command.DeliveryId, command.CourierId, command.OperatorId);
        return AssignCourierResult.Ok(delivery);
    }
}
