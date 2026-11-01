using LogiTracker.Application.Outbox;
using LogiTracker.Domain.Deliveries;
using LogiTracker.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace LogiTracker.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistencia del módulo Entregas. Cada módulo usa su propio esquema (ADR-001):
/// "deliveries" para Entregas y "access" para los operadores.
/// </summary>
public sealed class LogiTrackerDbContext : DbContext
{
    public const string DeliveriesSchema = "deliveries";
    public const string AccessSchema = "access";

    public LogiTrackerDbContext(DbContextOptions<LogiTrackerDbContext> options)
        : base(options)
    {
    }

    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<CourierRecord> Couriers => Set<CourierRecord>();
    public DbSet<OperatorRecord> Operators => Set<OperatorRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogiTrackerDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // PostgreSQL guarda instantes en UTC: se normaliza cualquier fecha antes de escribirla.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }
}
