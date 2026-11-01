using System.Data.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Domain.Deliveries;
using LogiTracker.Infrastructure.Persistence;
using LogiTracker.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;

namespace LogiTracker.Infrastructure.Deliveries;

/// <summary>
/// Adaptador de <see cref="ICourierAvailability"/> que lee la agenda interna de turnos (tabla deliveries.couriers).
/// Si mañana la fuente es un proveedor de transporte externo, solo se reemplaza este adaptador (EC-08).
/// </summary>
internal sealed class EfCourierAvailability : ICourierAvailability
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(3);

    private readonly LogiTrackerDbContext _db;

    public EfCourierAvailability(LogiTrackerDbContext db) => _db = db;

    public async Task<CourierAvailability?> GetAsync(CourierId courierId, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(QueryTimeout);
        try
        {
            var courier = await _db.Couriers
                .AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == courierId.Value, timeout.Token);

            return courier is null
                ? null
                : new CourierAvailability(courier.IsOnShift, courier.ActiveDeliveries, courier.MaxDeliveries);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AvailabilityUnavailableException("The courier availability source timed out.", exception);
        }
        catch (DbException exception)
        {
            throw new AvailabilityUnavailableException("The courier availability source failed.", exception);
        }
    }
}

/// <summary>
/// Decorador para inyectar fallas de forma controlada (ingeniería del caos, video 28).
/// Solo se registra cuando FaultInjection:Enabled es true (entorno de desarrollo).
/// </summary>
internal sealed class FaultInjectingCourierAvailability : ICourierAvailability
{
    private readonly ICourierAvailability _inner;
    private readonly FaultInjectionState _faults;

    public FaultInjectingCourierAvailability(ICourierAvailability inner, FaultInjectionState faults)
    {
        _inner = inner;
        _faults = faults;
    }

    public Task<CourierAvailability?> GetAsync(CourierId courierId, CancellationToken cancellationToken)
    {
        if (_faults.AvailabilityDown)
            throw new AvailabilityUnavailableException("Simulated outage of the courier availability source.");

        return _inner.GetAsync(courierId, cancellationToken);
    }
}

internal sealed class EfCourierQueries : ICourierQueries
{
    private readonly LogiTrackerDbContext _db;

    public EfCourierQueries(LogiTrackerDbContext db) => _db = db;

    public async Task<IReadOnlyList<CourierSummary>> ListAsync(CancellationToken cancellationToken) =>
        await _db.Couriers
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CourierSummary(c.Id, c.Code, c.Alias, c.IsOnShift, c.ActiveDeliveries, c.MaxDeliveries))
            .ToListAsync(cancellationToken);
}
