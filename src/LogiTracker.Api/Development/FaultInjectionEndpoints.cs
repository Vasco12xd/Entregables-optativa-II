using LogiTracker.Api.Security;
using LogiTracker.Infrastructure.Resilience;

namespace LogiTracker.Api.Development;

internal sealed record FaultInjectionRequest(bool AvailabilityDown, NotificationFaultMode NotificationFault);

/// <summary>
/// Herramienta de desarrollo para provocar fallas controladas y observar la respuesta del sistema
/// (ingeniería del caos acotada, video 28). Solo se publica si FaultInjection:Enabled es true.
/// </summary>
internal static class FaultInjectionEndpoints
{
    public static void MapFaultInjection(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/faults")
            .RequireAuthorization(policy => policy.RequireRole(ApiKeyAuthenticationHandler.OperatorRole))
            .WithTags("Development");

        group.MapGet("/", (FaultInjectionState state) =>
            Results.Ok(new FaultInjectionRequest(state.AvailabilityDown, state.NotificationFault)));

        group.MapPut("/", (FaultInjectionRequest request, FaultInjectionState state) =>
        {
            state.AvailabilityDown = request.AvailabilityDown;
            state.NotificationFault = request.NotificationFault;
            return Results.Ok(request);
        });
    }
}
