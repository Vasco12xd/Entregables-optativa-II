using LogiTracker.Infrastructure.Observability;
using LogiTracker.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogiTracker.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Aplica las migraciones pendientes al iniciar. Pensado para desarrollo y pruebas.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>Carga repartidores y operadores de demostración si la base está vacía. Solo desarrollo.</summary>
    public bool SeedDemoData { get; set; }
}

/// <summary>Datos fijos de demostración, referenciados también por LogiTracker.Api.http.</summary>
public static class DemoData
{
    public static readonly Guid AnaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid LuisId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid SofiaId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid PedroId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static readonly Guid MartaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid JorgeId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>Claves SOLO para el entorno local de demostración. En un entorno real se emiten fuera del código.</summary>
    public const string MartaDemoKey = "dev-key-marta-0001";
    public const string JorgeDemoKey = "dev-key-jorge-0002";

    /// <summary>
    /// Las cargas iniciales (por ejemplo, Ana con 3 de 5) representan entregas de otros turnos
    /// que no forman parte de la demostración.
    /// </summary>
    public static IReadOnlyList<CourierRecord> Couriers() =>
    [
        new() { Id = AnaId, Code = "CR-001", Alias = "Ana", IsOnShift = true, ActiveDeliveries = 3, MaxDeliveries = 5 },
        new() { Id = LuisId, Code = "CR-002", Alias = "Luis", IsOnShift = true, ActiveDeliveries = 1, MaxDeliveries = 5 },
        new() { Id = SofiaId, Code = "CR-003", Alias = "Sofia", IsOnShift = true, ActiveDeliveries = 5, MaxDeliveries = 5 },
        new() { Id = PedroId, Code = "CR-004", Alias = "Pedro", IsOnShift = false, ActiveDeliveries = 0, MaxDeliveries = 5 },
    ];

    public static IReadOnlyList<OperatorRecord> Operators() =>
    [
        new() { Id = MartaId, Alias = "Marta", ApiKeyHash = ApiKeyHasher.Hash(MartaDemoKey), IsActive = true },
        new() { Id = JorgeId, Alias = "Jorge", ApiKeyHash = ApiKeyHasher.Hash(JorgeDemoKey), IsActive = true },
    ];
}

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, DatabaseOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LogiTrackerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (options.ApplyMigrationsOnStartup)
        {
            await db.Database.MigrateAsync(cancellationToken);
            InfrastructureLog.MigrationsApplied(logger);
        }

        if (options.SeedDemoData && !await db.Couriers.AnyAsync(cancellationToken))
        {
            var couriers = DemoData.Couriers();
            var operators = DemoData.Operators();
            db.Couriers.AddRange(couriers);
            db.Operators.AddRange(operators);
            await db.SaveChangesAsync(cancellationToken);
            InfrastructureLog.DemoDataSeeded(logger, couriers.Count, operators.Count);
        }
    }
}
