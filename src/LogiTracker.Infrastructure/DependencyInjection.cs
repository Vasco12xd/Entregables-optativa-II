using LogiTracker.Application.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Notifications;
using LogiTracker.Application.Outbox;
using LogiTracker.Application.Security;
using LogiTracker.Infrastructure.Common;
using LogiTracker.Infrastructure.Deliveries;
using LogiTracker.Infrastructure.Notifications;
using LogiTracker.Infrastructure.Outbox;
using LogiTracker.Infrastructure.Persistence;
using LogiTracker.Infrastructure.Resilience;
using LogiTracker.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LogiTracker.Infrastructure;

/// <summary>Registra los adaptadores que implementan los puertos de la capa de aplicación.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "LogiTracker";

    public static IServiceCollection AddLogiTrackerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Missing connection string '{ConnectionStringName}'.");

        services.AddDbContext<LogiTrackerDbContext>(options => ConfigureDbContext(options, connectionString));

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<OutboxDispatcherOptions>(configuration.GetSection(OutboxDispatcherOptions.SectionName));

        var policy = new OutboxPolicy();
        configuration.GetSection(OutboxDispatcherOptions.SectionName).Bind(policy);
        services.AddSingleton(policy);

        // Adaptadores de los puertos
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IDeliveryRepository, EfDeliveryRepository>();
        services.AddScoped<IDeliveryEventPublisher, OutboxEventPublisher>();
        services.AddScoped<ICourierQueries, EfCourierQueries>();
        services.AddScoped<IOutboxStore, EfOutboxStore>();
        services.AddScoped<IOperatorAuthenticator, EfOperatorAuthenticator>();
        services.AddSingleton<FaultInjectionState>();
        services.AddSingleton<INotificationSender, SimulatedNotificationSender>();

        services.AddScoped<EfCourierAvailability>();
        if (configuration.GetValue<bool>("FaultInjection:Enabled"))
        {
            services.AddScoped<ICourierAvailability>(sp => new FaultInjectingCourierAvailability(
                sp.GetRequiredService<EfCourierAvailability>(),
                sp.GetRequiredService<FaultInjectionState>()));
        }
        else
        {
            services.AddScoped<ICourierAvailability>(sp => sp.GetRequiredService<EfCourierAvailability>());
        }

        if (configuration.GetValue("Outbox:DispatcherEnabled", defaultValue: true))
            services.AddHostedService<OutboxDispatcherService>();

        return services;
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", LogiTrackerDbContext.DeliveriesSchema))
            .UseSnakeCaseNamingConvention();
}
