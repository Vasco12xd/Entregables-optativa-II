using LogiTracker.Api.Contracts;
using LogiTracker.Api.Security;
using LogiTracker.Application.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Outbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogiTracker.Api.Controllers;

/// <summary>Lista los repartidores con su turno y su carga, para que el operador elija a quién asignar.</summary>
[ApiController]
[Route("api/couriers")]
[Authorize(Roles = ApiKeyAuthenticationHandler.OperatorRole)]
[Produces("application/json")]
public sealed class CouriersController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CourierSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] ICourierQueries queries, CancellationToken cancellationToken) =>
        Ok(await queries.ListAsync(cancellationToken));
}

/// <summary>
/// Revisión de avisos apartados (Dead Letter Queue) y reprocesamiento controlado (video 21).
/// </summary>
[ApiController]
[Route("api/outbox/dead-letters")]
[Authorize(Roles = ApiKeyAuthenticationHandler.OperatorRole)]
[Produces("application/json")]
public sealed class DeadLettersController : ControllerBase
{
    private const int MaxListed = 100;

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeadLetterResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromServices] IOutboxStore store, CancellationToken cancellationToken)
    {
        var messages = await store.ListDeadLettersAsync(MaxListed, cancellationToken);
        return Ok(messages.Select(m => new DeadLetterResponse(m.Id, m.EventType, m.Attempts, m.LastError, m.OccurredAt)));
    }

    /// <summary>Devuelve el aviso a la cola para un nuevo intento, una vez corregida la causa.</summary>
    [HttpPost("{messageId:guid}/requeue")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Requeue(
        Guid messageId,
        [FromServices] IOutboxStore store,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        var requeued = await store.RequeueAsync(messageId, clock.UtcNow, cancellationToken);
        return requeued
            ? Accepted()
            : Problem(statusCode: StatusCodes.Status404NotFound, title: "El aviso no existe o no está apartado.");
    }
}
