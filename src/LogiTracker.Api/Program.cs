using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LogiTracker.Api;
using LogiTracker.Api.Development;
using LogiTracker.Api.Errors;
using LogiTracker.Api.Observability;
using LogiTracker.Api.Security;
using LogiTracker.Application.Observability;
using LogiTracker.Infrastructure;
using LogiTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Registros estructurados: incluyen el identificador de correlación de cada solicitud ---
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });
else
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

// --- Capas: aplicación (casos de uso) e infraestructura (adaptadores) ---
builder.Services.AddLogiTrackerApplication();
builder.Services.AddLogiTrackerInfrastructure(builder.Configuration);

// --- Entrada HTTP ---
builder.Services
    .AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ErrorResponses.AddCorrelationId);

// --- Seguridad: clave de API por operador y límite de solicitudes ---
builder.Services
    .AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitsPerMinute", 300),
                Window = TimeSpan.FromMinutes(1),
            }));
});

// --- Observabilidad: trazas y métricas con OpenTelemetry; salud de la base de datos ---
builder.Services.AddHealthChecks().AddDbContextCheck<LogiTrackerDbContext>("database");
var consoleExporter = builder.Configuration.GetValue<bool>("Observability:ConsoleExporter");
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("LogiTracker.Api"))
    .WithTracing(tracing =>
    {
        tracing.AddSource(LogiTrackerTelemetry.SourceName).AddAspNetCoreInstrumentation();
        if (consoleExporter)
            tracing.AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(LogiTrackerTelemetry.SourceName).AddAspNetCoreInstrumentation();
        if (consoleExporter)
            metrics.AddConsoleExporter();
    });

// --- Documentación OpenAPI con el esquema de autenticación ---
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info.Title = "LogiTracker API";
    document.Info.Description = "Vertical slice de asignación de repartidores (Actividad 4).";
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes[ApiKeyAuthenticationHandler.SchemeName] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = ApiKeyAuthenticationHandler.HeaderName,
        Description = "Clave de API del operador.",
    };
    document.Security =
    [
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiKeyAuthenticationHandler.SchemeName, document)] = [],
        },
    ];
    return Task.CompletedTask;
}));

var app = builder.Build();

// --- Migraciones reproducibles y datos de demostración (según configuración) ---
var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
await DatabaseInitializer.InitializeAsync(app.Services, databaseOptions);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.Title = "LogiTracker API");
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

if (builder.Configuration.GetValue<bool>("FaultInjection:Enabled"))
    app.MapFaultInjection();

await app.RunAsync();

/// <summary>Punto de entrada; parcial para que las pruebas de integración puedan levantar la API.</summary>
public partial class Program;
