using System.Net.Http.Json;
using LogiTracker.Application.Outbox;
using LogiTracker.Infrastructure.Persistence;
using LogiTracker.Infrastructure.Resilience;
using LogiTracker.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace LogiTracker.IntegrationTests;

/// <summary>
/// Levanta un PostgreSQL real y efímero en Docker y la API completa en memoria.
/// Las migraciones se aplican sobre la base vacía al iniciar: así se comprueba que son reproducibles.
/// </summary>
public sealed class LogiTrackerApiFixture : IAsyncLifetime
{
    public const string OperatorKey = "integration-test-operator-key";
    public static readonly Guid OperatorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Fixture not initialized.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:LogiTracker", _postgres.GetConnectionString());
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
            builder.UseSetting("Database:SeedDemoData", "false");
            // El despachador se ejecuta manualmente para que las pruebas sean deterministas.
            builder.UseSetting("Outbox:DispatcherEnabled", "false");
            builder.UseSetting("Outbox:MaxAttempts", "2");
            builder.UseSetting("Outbox:BaseRetryDelay", "00:00:00");
            builder.UseSetting("FaultInjection:Enabled", "false");
            builder.UseSetting("RateLimiting:PermitsPerMinute", "10000");
        });

        // Arranca la API (aplica migraciones) y registra un operador de prueba.
        _ = _factory.Server;
        await WithDbAsync(async db =>
        {
            db.Operators.Add(new OperatorRecord { Id = OperatorId, Alias = "Tester", ApiKeyHash = ApiKeyHasher.Hash(OperatorKey), IsActive = true });
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateOperatorClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", OperatorKey);
        return client;
    }

    public async Task WithDbAsync(Func<LogiTrackerDbContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<LogiTrackerDbContext>());
    }

    public async Task<T> WithDbAsync<T>(Func<LogiTrackerDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<LogiTrackerDbContext>());
    }

    public async Task<Guid> SeedCourierAsync(bool isOnShift, int activeDeliveries, int maxDeliveries)
    {
        var id = Guid.NewGuid();
        await WithDbAsync(async db =>
        {
            db.Couriers.Add(new CourierRecord
            {
                Id = id,
                Code = $"T-{id.ToString("N")[..8]}",
                Alias = "Test",
                IsOnShift = isOnShift,
                ActiveDeliveries = activeDeliveries,
                MaxDeliveries = maxDeliveries,
            });
            await db.SaveChangesAsync();
        });
        return id;
    }

    public Task<int> CourierLoadAsync(Guid courierId) =>
        WithDbAsync(db => db.Couriers.Where(c => c.Id == courierId).Select(c => c.ActiveDeliveries).SingleAsync());

    public Task<List<OutboxMessage>> OutboxMessagesForAsync(Guid deliveryId) =>
        WithDbAsync(db => db.OutboxMessages
            .FromSql($"SELECT * FROM deliveries.outbox_messages WHERE payload->>'deliveryId' = {deliveryId.ToString()}")
            .AsNoTracking()
            .ToListAsync());

    public async Task<DispatchSummary> RunDispatcherAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<DispatchOutboxUseCase>().ExecuteAsync(CancellationToken.None);
    }

    public FaultInjectionState Faults => Factory.Services.GetRequiredService<FaultInjectionState>();

    public static async Task<Guid> CreateDeliveryAsync(HttpClient client, Guid? orderId = null)
    {
        var response = await client.PostAsJsonAsync("/api/deliveries", NewDeliveryRequest(orderId ?? Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CreatedDelivery>();
        return body!.DeliveryId;
    }

    public static object NewDeliveryRequest(Guid orderId) => new
    {
        orderId,
        address = new { street = "Calle 10 # 20-30", city = "Bogota", postalCode = "110111" },
        windowStart = "2026-10-31T14:00:00-05:00",
        windowEnd = "2026-10-31T18:00:00-05:00",
        packageCount = 2,
    };

    public sealed record CreatedDelivery(Guid DeliveryId, bool Created);
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<LogiTrackerApiFixture>
{
    public const string Name = "LogiTracker API";
}
