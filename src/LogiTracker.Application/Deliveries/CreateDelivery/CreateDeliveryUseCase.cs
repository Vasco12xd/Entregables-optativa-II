using LogiTracker.Application.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Observability;
using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;
using Microsoft.Extensions.Logging;

namespace LogiTracker.Application.Deliveries.CreateDelivery;

/// <summary>Contrato C-01 "Pedido listo para logística". No incluye precio ni datos de pago.</summary>
public sealed record CreateDeliveryCommand(
    Guid OrderId,
    string Street,
    string City,
    string PostalCode,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int PackageCount);

/// <param name="Created">false cuando la entrega ya existía para ese pedido (reintento idempotente).</param>
public sealed record CreateDeliveryResult(bool Succeeded, string? ErrorCode, Guid? DeliveryId, bool Created)
{
    public static CreateDeliveryResult Ok(DeliveryId id, bool created)
    {
        ArgumentNullException.ThrowIfNull(id);
        return new(true, null, id.Value, created);
    }

    public static CreateDeliveryResult Fail(string errorCode) => new(false, errorCode, null, false);
}

/// <summary>
/// Crea una entrega pendiente de asignación a partir de un pedido listo para logística.
/// Es idempotente por pedido: si el canal reenvía el mismo pedido, no se crea una segunda entrega (riesgo R2).
/// </summary>
public sealed class CreateDeliveryUseCase
{
    private readonly IDeliveryRepository _deliveries;
    private readonly IClock _clock;
    private readonly ILogger<CreateDeliveryUseCase> _logger;

    public CreateDeliveryUseCase(IDeliveryRepository deliveries, IClock clock, ILogger<CreateDeliveryUseCase> logger)
    {
        _deliveries = deliveries;
        _clock = clock;
        _logger = logger;
    }

    public async Task<CreateDeliveryResult> ExecuteAsync(CreateDeliveryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var activity = LogiTrackerTelemetry.ActivitySource.StartActivity("CreateDelivery");

        Delivery delivery;
        try
        {
            var orderId = new OrderId(command.OrderId);
            var existing = await _deliveries.GetByOrderIdAsync(orderId, cancellationToken);
            if (existing is not null)
            {
                ApplicationLog.DeliveryAlreadyExisted(_logger, existing.Id.Value, command.OrderId);
                return CreateDeliveryResult.Ok(existing.Id, created: false);
            }

            delivery = Delivery.CreateFromOrder(
                DeliveryId.New(),
                orderId,
                new DeliveryAddress(command.Street, command.City, command.PostalCode),
                new DeliveryWindow(command.WindowStart, command.WindowEnd),
                command.PackageCount,
                _clock.UtcNow);
        }
        catch (DomainRuleViolationException violation)
        {
            return CreateDeliveryResult.Fail(violation.Code);
        }

        try
        {
            await _deliveries.AddAsync(delivery, cancellationToken);
        }
        catch (DuplicateOrderException)
        {
            // Otra solicitud creó la entrega del mismo pedido al mismo tiempo: se devuelve la existente.
            var existing = await _deliveries.GetByOrderIdAsync(delivery.OrderId, cancellationToken);
            return CreateDeliveryResult.Ok(existing!.Id, created: false);
        }

        activity?.SetTag("delivery.id", delivery.Id.Value);
        ApplicationLog.DeliveryCreated(_logger, delivery.Id.Value, command.OrderId);
        return CreateDeliveryResult.Ok(delivery.Id, created: true);
    }
}
