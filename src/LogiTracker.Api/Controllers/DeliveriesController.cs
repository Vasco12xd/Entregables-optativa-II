using LogiTracker.Api.Contracts;
using LogiTracker.Api.Errors;
using LogiTracker.Api.Security;
using LogiTracker.Application.Deliveries;
using LogiTracker.Application.Deliveries.AssignCourier;
using LogiTracker.Application.Deliveries.CreateDelivery;
using LogiTracker.Application.Deliveries.GetDelivery;
using LogiTracker.Application.Deliveries.ReleaseCourier;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogiTracker.Api.Controllers;

/// <summary>
/// Adaptador de entrada HTTP del contexto de Entregas. Traduce HTTP a comandos y resultados a respuestas;
/// no contiene reglas de negocio.
/// </summary>
[ApiController]
[Route("api/deliveries")]
[Authorize(Roles = ApiKeyAuthenticationHandler.OperatorRole)]
[Produces("application/json")]
public sealed class DeliveriesController : ControllerBase
{
    /// <summary>Registra una entrega a partir de un pedido listo para logística. Idempotente por pedido.</summary>
    [HttpPost]
    [ProducesResponseType<CreateDeliveryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CreateDeliveryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateDeliveryRequest request,
        [FromServices] CreateDeliveryUseCase useCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = new CreateDeliveryCommand(
            request.OrderId!.Value,
            request.Address!.Street!,
            request.Address.City!,
            request.Address.PostalCode!,
            request.WindowStart!.Value,
            request.WindowEnd!.Value,
            request.PackageCount);

        var result = await useCase.ExecuteAsync(command, cancellationToken);
        if (!result.Succeeded)
            return this.BusinessError(result.ErrorCode!);

        var response = new CreateDeliveryResponse(result.DeliveryId!.Value, result.Created);
        return result.Created
            ? CreatedAtAction(nameof(GetById), new { deliveryId = response.DeliveryId }, response)
            : Ok(response);
    }

    /// <summary>Consulta el estado, el repartidor activo y el historial de asignaciones.</summary>
    [HttpGet("{deliveryId:guid}")]
    [ProducesResponseType<DeliveryDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid deliveryId,
        [FromServices] GetDeliveryQuery query,
        CancellationToken cancellationToken)
    {
        var details = await query.ExecuteAsync(deliveryId, cancellationToken);
        return details is null ? this.BusinessError(ApplicationErrors.DeliveryNotFound) : Ok(details);
    }

    /// <summary>Asigna un repartidor a la entrega (AssignCourierUseCase).</summary>
    [HttpPost("{deliveryId:guid}/assignment")]
    [ProducesResponseType<AssignCourierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Assign(
        Guid deliveryId,
        AssignCourierRequest request,
        [FromServices] AssignCourierUseCase useCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = new AssignCourierCommand(deliveryId, request.CourierId!.Value, User.GetOperatorId());

        var result = await useCase.ExecuteAsync(command, cancellationToken);
        if (!result.Succeeded)
            return this.BusinessError(result.ErrorCode!);

        return Ok(new AssignCourierResponse(result.DeliveryId, result.CourierId!.Value, result.Status!, result.AssignedAt!.Value));
    }

    /// <summary>Libera al repartidor activo con un motivo obligatorio.</summary>
    [HttpPost("{deliveryId:guid}/assignment/release")]
    [ProducesResponseType<ReleaseCourierResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Release(
        Guid deliveryId,
        ReleaseCourierRequest request,
        [FromServices] ReleaseCourierUseCase useCase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var command = new ReleaseCourierCommand(deliveryId, request.Reason ?? string.Empty, User.GetOperatorId());

        var result = await useCase.ExecuteAsync(command, cancellationToken);
        if (!result.Succeeded)
            return this.BusinessError(result.ErrorCode!);

        return Ok(new ReleaseCourierResponse(result.DeliveryId, result.Status!));
    }
}
