using LogiTracker.Application.Outbox;
using LogiTracker.Infrastructure.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogiTracker.Infrastructure.Outbox;

public sealed class OutboxDispatcherOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Si es false, el despachador no se ejecuta (las pruebas de integración lo invocan manualmente).</summary>
    public bool DispatcherEnabled { get; set; } = true;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);
}

/// <summary>
/// Proceso en segundo plano dentro del mismo despliegue (ADR-001): consume la bandeja de salida periódicamente.
/// </summary>
internal sealed class OutboxDispatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxDispatcherOptions _options;
    private readonly ILogger<OutboxDispatcherService> _logger;

    public OutboxDispatcherService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxDispatcherOptions> options,
        ILogger<OutboxDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.PollingInterval);
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<DispatchOutboxUseCase>();
                await dispatcher.ExecuteAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // El despachador nunca debe detener la aplicación; el error queda registrado para investigarlo.
                InfrastructureLog.DispatcherCycleFailed(_logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
