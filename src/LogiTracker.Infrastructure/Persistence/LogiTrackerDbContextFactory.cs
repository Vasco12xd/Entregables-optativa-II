using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LogiTracker.Infrastructure.Persistence;

/// <summary>
/// Usada solo por las herramientas de EF Core para crear migraciones (dotnet ef migrations add).
/// La cadena de conexión se toma de la variable de entorno LOGITRACKER_CONNECTION o de la base local de desarrollo.
/// </summary>
public sealed class LogiTrackerDbContextFactory : IDesignTimeDbContextFactory<LogiTrackerDbContext>
{
    private const string LocalDevelopmentConnection =
        "Host=localhost;Port=5432;Database=logitracker;Username=logitracker;Password=logitracker_dev";

    public LogiTrackerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("LOGITRACKER_CONNECTION") ?? LocalDevelopmentConnection;
        var options = new DbContextOptionsBuilder<LogiTrackerDbContext>();
        DependencyInjection.ConfigureDbContext(options, connectionString);
        return new LogiTrackerDbContext(options.Options);
    }
}
