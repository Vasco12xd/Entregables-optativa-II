using System.Net;
using System.Net.Http.Json;
using LogiTracker.Application.Outbox;
using LogiTracker.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;

namespace LogiTracker.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class OperationsAndSecurityTests
{
    private readonly LogiTrackerApiFixture _fixture;

    public OperationsAndSecurityTests(LogiTrackerApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrations_build_the_schema_from_an_empty_database()
    {
        var (applied, pending) = await _fixture.WithDbAsync(async db =>
            ((await db.Database.GetAppliedMigrationsAsync()).ToList(), (await db.Database.GetPendingMigrationsAsync()).ToList()));

        Assert.Contains(applied, m => m.EndsWith("_InitialDeliveriesSchema", StringComparison.Ordinal));
        Assert.Empty(pending);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a-key-that-does-not-exist")]
    public async Task RS_01_requests_without_a_valid_api_key_are_rejected(string? apiKey)
    {
        var client = _fixture.Factory.CreateClient();
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var response = await client.PostAsJsonAsync($"/api/deliveries/{Guid.NewGuid()}/assignment", new { courierId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_reports_the_database_and_echoes_a_supplied_correlation_id()
    {
        var client = _fixture.Factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "support-case-12345");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("support-case-12345", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Outbox_dispatcher_delivers_a_pending_notification()
    {
        var client = _fixture.CreateOperatorClient();
        var courier = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(client);
        await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assignment", new { courierId = courier });

        await _fixture.RunDispatcherAsync();

        var message = Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));
        Assert.Equal(OutboxStatus.Processed, message.Status);
        Assert.NotNull(message.ProcessedAt);
    }

    [Fact]
    public async Task Notification_outage_keeps_the_assignment_and_moves_the_notice_to_dead_letters_after_retries()
    {
        var client = _fixture.CreateOperatorClient();
        var courier = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(client);
        await _fixture.RunDispatcherAsync();   // Vacía mensajes pendientes de otras pruebas
        _fixture.Faults.NotificationFault = NotificationFaultMode.Transient;
        try
        {
            var assign = await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assignment", new { courierId = courier });
            Assert.Equal(HttpStatusCode.OK, assign.StatusCode);   // La falla externa no revierte la asignación

            await _fixture.RunDispatcherAsync();   // Intento 1: se programa reintento
            await _fixture.RunDispatcherAsync();   // Intento 2 (máximo en pruebas): se aparta

            var message = Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));
            Assert.Equal(OutboxStatus.DeadLettered, message.Status);
            Assert.Equal(2, message.Attempts);
            Assert.Contains("NotificationTransientException", message.LastError, StringComparison.Ordinal);
        }
        finally
        {
            _fixture.Faults.NotificationFault = NotificationFaultMode.None;
        }

        // Corregida la causa, el reprocesamiento controlado entrega el aviso
        var deadLetter = Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));
        var requeue = await client.PostAsync($"/api/outbox/dead-letters/{deadLetter.Id}/requeue", null);
        Assert.Equal(HttpStatusCode.Accepted, requeue.StatusCode);
        await _fixture.RunDispatcherAsync();
        Assert.Equal(OutboxStatus.Processed, Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId)).Status);
    }

    [Fact]
    public async Task RS_03_outbox_payload_carries_identifiers_only_and_no_personal_data()
    {
        var client = _fixture.CreateOperatorClient();
        var courier = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(client);
        await client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assignment", new { courierId = courier });

        var message = Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));

        Assert.DoesNotContain("Calle", message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bogota", message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(deliveryId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RS_01_api_keys_are_stored_only_as_hashes()
    {
        var stored = await _fixture.WithDbAsync(db => db.Operators.Select(o => o.ApiKeyHash).ToListAsync());

        Assert.DoesNotContain(LogiTrackerApiFixture.OperatorKey, stored);
        Assert.All(stored, hash => Assert.Equal(64, hash.Trim().Length));
    }
}
