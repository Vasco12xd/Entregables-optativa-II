using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Domain.Deliveries;
using LogiTracker.Infrastructure.Persistence;
using LogiTracker.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LogiTracker.Infrastructure.Deliveries;

/// <summary>Adaptador de <see cref="IDeliveryRepository"/> sobre PostgreSQL.</summary>
internal sealed class EfDeliveryRepository : IDeliveryRepository
{
    private const string UniqueViolation = "23505";

    private readonly LogiTrackerDbContext _db;

    public EfDeliveryRepository(LogiTrackerDbContext db) => _db = db;

    public Task<Delivery?> GetAsync(DeliveryId id, CancellationToken cancellationToken) =>
        _db.Deliveries
            .Include(d => d.History)
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<Delivery?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken) =>
        _db.Deliveries
            .Include(d => d.History)
            .SingleOrDefaultAsync(d => d.OrderId == orderId, cancellationToken);

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        _db.Deliveries.Add(delivery);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            _db.Entry(delivery).State = EntityState.Detached;
            throw new DuplicateOrderException("A delivery already exists for this order.", exception);
        }
    }

    /// <summary>
    /// Guarda la entrega, ajusta la carga del repartidor y confirma los hechos de la bandeja de salida en una transacción.
    /// La actualización condicional de la carga cierra la limitación de capacidad del ADR-002 (ver ADR-003):
    /// si dos entregas distintas se asignan al mismo repartidor a la vez, PostgreSQL serializa la fila del repartidor
    /// y la segunda actualización no encuentra cupo.
    /// </summary>
    public async Task SaveAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var domainEvent in delivery.DomainEvents)
            {
                switch (domainEvent)
                {
                    case CourierAssigned assigned:
                        var reserved = await _db.Couriers
                            .Where(c => c.Id == assigned.CourierId.Value && c.IsOnShift && c.ActiveDeliveries < c.MaxDeliveries)
                            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveDeliveries, c => c.ActiveDeliveries + 1), cancellationToken);
                        if (reserved == 0)
                            throw new CourierCapacityExceededException("The courier has no free capacity or is no longer on shift.");
                        break;

                    case CourierReleased released:
                        await _db.Couriers
                            .Where(c => c.Id == released.CourierId.Value && c.ActiveDeliveries > 0)
                            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ActiveDeliveries, c => c.ActiveDeliveries - 1), cancellationToken);
                        break;
                }
            }

            _db.Entry(delivery).Property<int>(DeliveryConfiguration.VersionProperty).CurrentValue += 1;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException("The delivery was modified by another operation.", exception);
        }
    }
}
