using LogiTracker.Api.Observability;
using LogiTracker.Application.Deliveries;
using LogiTracker.Domain.Deliveries;
using Microsoft.AspNetCore.Mvc;

namespace LogiTracker.Api.Errors;

/// <summary>
/// Traduce el catálogo de errores de negocio a respuestas HTTP (ADR-002 D-08).
/// La regla vive en el dominio; aquí solo se decide el código HTTP y un mensaje claro.
/// </summary>
internal static class ErrorResponses
{
    public static int StatusFor(string code) => code switch
    {
        ApplicationErrors.DeliveryNotFound or ApplicationErrors.CourierNotFound
            => StatusCodes.Status404NotFound,
        DeliveryErrors.DeliveryAlreadyAssigned or DeliveryErrors.DeliveryNotAssignable
            or DeliveryErrors.DeliveryNotReleasable or ApplicationErrors.ConcurrencyConflict
            => StatusCodes.Status409Conflict,
        DeliveryErrors.CourierUnavailable or DeliveryErrors.CourierCapacityReached
            => StatusCodes.Status422UnprocessableEntity,
        ApplicationErrors.AvailabilityServiceUnavailable
            => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status400BadRequest,
    };

    public static string TitleFor(string code) => code switch
    {
        ApplicationErrors.DeliveryNotFound => "Entrega no encontrada.",
        ApplicationErrors.CourierNotFound => "Repartidor no encontrado.",
        DeliveryErrors.DeliveryAlreadyAssigned => "La entrega ya tiene repartidor asignado.",
        DeliveryErrors.DeliveryNotAssignable => "La entrega no admite asignación en su estado actual.",
        DeliveryErrors.DeliveryNotReleasable => "La entrega no tiene un repartidor que se pueda liberar.",
        ApplicationErrors.ConcurrencyConflict => "La entrega cambió mientras se procesaba; vuelva a consultarla.",
        DeliveryErrors.CourierUnavailable => "El repartidor no está disponible (fuera de turno).",
        DeliveryErrors.CourierCapacityReached => "Capacidad del repartidor alcanzada.",
        ApplicationErrors.AvailabilityServiceUnavailable => "No se pudo consultar la disponibilidad del repartidor. Intente de nuevo.",
        DeliveryErrors.ReleaseReasonRequired => "Se requiere un motivo para liberar al repartidor.",
        DeliveryErrors.InvalidDeliveryWindow => "La ventana de entrega no es válida: el inicio debe ser anterior al fin.",
        DeliveryErrors.InvalidAddress => "La dirección de entrega está incompleta.",
        DeliveryErrors.InvalidPackageCount => "El número de paquetes debe ser mayor que cero.",
        DeliveryErrors.InvalidIdentifier => "Identificador no válido.",
        _ => "La solicitud no se pudo procesar.",
    };

    public static ObjectResult BusinessError(this ControllerBase controller, string code)
    {
        ArgumentNullException.ThrowIfNull(controller);
        var status = StatusFor(code);
        var problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: status,
            title: TitleFor(code),
            type: $"https://logitracker.local/errors/{code}");
        problem.Extensions["code"] = code;

        if (status == StatusCodes.Status503ServiceUnavailable)
            controller.Response.Headers.RetryAfter = "5";

        return new ObjectResult(problem) { StatusCode = status };
    }

    /// <summary>Añade el identificador de correlación a toda respuesta de error (incluidas las de validación).</summary>
    public static void AddCorrelationId(ProblemDetailsContext context)
    {
        if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var correlationId))
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
    }
}
