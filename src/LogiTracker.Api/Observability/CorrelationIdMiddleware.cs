using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LogiTracker.Api.Observability;

/// <summary>
/// Asigna a cada solicitud un identificador de correlación que aparece en todos sus registros,
/// en la respuesta (cabecera X-Correlation-Id) y en los errores. Soporte puede pedirlo para investigar un caso.
/// </summary>
internal sealed partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    private static readonly Func<ILogger, string, IDisposable?> CorrelationScope =
        LoggerMessage.DefineScope<string>("CorrelationId:{CorrelationId}");

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        // Solo se acepta un identificador corto y alfanumérico: evita inyectar texto arbitrario en los registros.
        var correlationId = IsSafe(incoming)
            ? incoming
            : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (CorrelationScope(_logger, correlationId))
        {
            await _next(context);
        }
    }

    private static bool IsSafe(string value) => !string.IsNullOrEmpty(value) && SafeId().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
