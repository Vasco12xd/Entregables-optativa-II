using LogiTracker.Application.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Observability;
using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;
using Microsoft.Extensions.Logging;

namespace LogiTracker.Application.Deliveries.ReleaseCourier;

public sealed record ReleaseCourierCommand(Guid DeliveryId, string Reason, Guid OperatorId);

public sealed record ReleaseCourierResult(bool Succeeded, string? ErrorCode, Guid DeliveryId, string? Status)
{
    public static ReleaseCourierResult Ok(Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        return new(true, null, delivery.Id.Value, delivery.Status.ToString());
    }

    public static ReleaseCourierResult Fail(Guid deliveryId, string errorCode) => new(false, errorCode, deliveryId, null);
}

/// <summary>Quita el repartidor de una entrega asignada, con motivo obligatorio (INV-07, escenarios ESC-10 y ESC-11).</summary>
public sealed class ReleaseCourierUseCase
{
    private readonly IDeliveryRepository _deliveries;
    private readonly IDeliveryEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger<ReleaseCourierUseCase> _logger;

    public ReleaseCourierUseCase(
        IDeliveryRepository deliveries,
        IDeliveryEventPublisher publisher,
        IClock clock,
        ILogger<ReleaseCourierUseCase> logger)
    {
        _deliveries = deliveries;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ReleaseCourierResult> ExecuteAsync(ReleaseCourierCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var activity = LogiTrackerTelemetry.ActivitySource.StartActivity("ReleaseCourier");
        activity?.SetTag("delivery.id", command.DeliveryId);

        var result = await ExecuteCoreAsync(command, cancellationToken);
        if (result.Succeeded)
        {
            ApplicationLog.CourierReleased(_logger, command.DeliveryId, command.OperatorId);
        }
        else
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, result.ErrorCode);
            ApplicationLog.ReleaseRejected(_logger, command.DeliveryId, result.ErrorCode!);
        }

        return result;
    }

    private async Task<ReleaseCourierResult> ExecuteCoreAsync(ReleaseCourierCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deliveryId = new DeliveryId(command.DeliveryId);
            var operatorId = new OperatorId(command.OperatorId);
            var reason = new ReleaseReason(command.Reason);

            var delivery = await _deliveries.GetAsync(deliveryId, cancellationToken);
            if (delivery is null)
                return ReleaseCourierResult.Fail(command.DeliveryId, ApplicationErrors.DeliveryNotFound);

            delivery.ReleaseCourier(reason, operatorId, _clock.UtcNow);
            await _publisher.PublishAsync(delivery.DomainEvents, cancellationToken);
            await _deliveries.SaveAsync(delivery, cancellationToken);
            delivery.ClearDomainEvents();
            return ReleaseCourierResult.Ok(delivery);
        }
        catch (DomainRuleViolationException violation)
        {
            return ReleaseCourierResult.Fail(command.DeliveryId, violation.Code);
        }
        catch (ConcurrencyConflictException)
        {
            return ReleaseCourierResult.Fail(command.DeliveryId, ApplicationErrors.ConcurrencyConflict);
        }
    }
}
