using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LogiTracker.Application.Outbox;
using Microsoft.EntityFrameworkCore;

namespace LogiTracker.IntegrationTests;

/// <summary>
/// Pruebas de integración del vertical slice: HTTP → controlador → caso de uso → dominio → PostgreSQL.
/// Cada prueba crea sus propios repartidores y entregas, así que no dependen del orden de ejecución.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssignCourierFlowTests
{
    private readonly LogiTrackerApiFixture _fixture;
    private readonly HttpClient _client;

    public AssignCourierFlowTests(LogiTrackerApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateOperatorClient();
    }

    [Fact]
    public async Task ESC_01_assigns_a_courier_end_to_end_and_records_everything_in_one_transaction()
    {
        var courier = await _fixture.SeedCourierAsync(isOnShift: true, activeDeliveries: 3, maxDeliveries: 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);

        var response = await AssignAsync(deliveryId, courier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await GetDeliveryAsync(deliveryId);
        Assert.Equal("Assigned", details.GetProperty("status").GetString());
        Assert.Equal(courier, details.GetProperty("activeCourierId").GetGuid());
        var assignment = Assert.Single(details.GetProperty("history").EnumerateArray());
        // El operador sale de la clave de API, no del cuerpo de la petición (RS-02)
        Assert.Equal(LogiTrackerApiFixture.OperatorId, assignment.GetProperty("assignedBy").GetGuid());
        Assert.Equal(4, await _fixture.CourierLoadAsync(courier));
        var message = Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));
        Assert.Equal("CourierAssigned", message.EventType);
        Assert.Equal(OutboxStatus.Pending, message.Status);
    }

    [Fact]
    public async Task ESC_05_rejects_a_saturated_courier_with_422_and_changes_nothing()
    {
        var courier = await _fixture.SeedCourierAsync(isOnShift: true, activeDeliveries: 5, maxDeliveries: 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);

        var response = await AssignAsync(deliveryId, courier);

        await AssertBusinessErrorAsync(response, HttpStatusCode.UnprocessableEntity, "courier_capacity_reached");
        Assert.Equal("PendingAssignment", (await GetDeliveryAsync(deliveryId)).GetProperty("status").GetString());
        Assert.Empty(await _fixture.OutboxMessagesForAsync(deliveryId));
        Assert.Equal(5, await _fixture.CourierLoadAsync(courier));
    }

    [Fact]
    public async Task ESC_04_rejects_a_courier_off_shift_with_422()
    {
        var courier = await _fixture.SeedCourierAsync(isOnShift: false, activeDeliveries: 0, maxDeliveries: 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);

        var response = await AssignAsync(deliveryId, courier);

        await AssertBusinessErrorAsync(response, HttpStatusCode.UnprocessableEntity, "courier_unavailable");
    }

    [Fact]
    public async Task ESC_02_rejects_a_second_courier_with_409()
    {
        var luis = await _fixture.SeedCourierAsync(true, 0, 5);
        var ana = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);
        await AssignAsync(deliveryId, luis);

        var response = await AssignAsync(deliveryId, ana);

        await AssertBusinessErrorAsync(response, HttpStatusCode.Conflict, "delivery_already_assigned");
        Assert.Equal(0, await _fixture.CourierLoadAsync(ana));
    }

    [Fact]
    public async Task ESC_03_retrying_the_same_assignment_does_not_duplicate_load_or_notifications()
    {
        var courier = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);

        var first = await AssignAsync(deliveryId, courier);
        var retry = await AssignAsync(deliveryId, courier);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Single((await GetDeliveryAsync(deliveryId)).GetProperty("history").EnumerateArray());
        Assert.Equal(1, await _fixture.CourierLoadAsync(courier));
        Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));
    }

    [Fact]
    public async Task ESC_08_concurrent_assignments_of_the_same_delivery_leave_exactly_one_courier()
    {
        var ana = await _fixture.SeedCourierAsync(true, 0, 5);
        var luis = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);

        var responses = await Task.WhenAll(AssignAsync(deliveryId, ana), AssignAsync(deliveryId, luis));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await GetDeliveryAsync(deliveryId)).GetProperty("history").EnumerateArray());
        Assert.Equal(1, await _fixture.CourierLoadAsync(ana) + await _fixture.CourierLoadAsync(luis));
        Assert.Single(await _fixture.OutboxMessagesForAsync(deliveryId));
    }

    [Fact]
    public async Task INV_04_concurrent_assignments_of_different_deliveries_never_exceed_courier_capacity()
    {
        // Limitación reconocida en el ADR-002 y cerrada en el ADR-003 con una guarda atómica en la base.
        var courier = await _fixture.SeedCourierAsync(isOnShift: true, activeDeliveries: 4, maxDeliveries: 5);
        var first = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);
        var second = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);

        var responses = await Task.WhenAll(AssignAsync(first, courier), AssignAsync(second, courier));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        var rejected = Assert.Single(responses, r => r.StatusCode != HttpStatusCode.OK);
        await AssertBusinessErrorAsync(rejected, HttpStatusCode.UnprocessableEntity, "courier_capacity_reached");
        Assert.Equal(5, await _fixture.CourierLoadAsync(courier));
    }

    [Fact]
    public async Task ESC_10_releasing_with_reason_frees_the_courier_and_publishes_the_fact()
    {
        var courier = await _fixture.SeedCourierAsync(true, 2, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);
        await AssignAsync(deliveryId, courier);

        var response = await _client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assignment/release", new { reason = "Vehiculo averiado" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await GetDeliveryAsync(deliveryId);
        Assert.Equal("PendingAssignment", details.GetProperty("status").GetString());
        Assert.Equal("Vehiculo averiado", details.GetProperty("history")[0].GetProperty("releaseReason").GetString());
        Assert.Equal(2, await _fixture.CourierLoadAsync(courier));
        Assert.Contains(await _fixture.OutboxMessagesForAsync(deliveryId), m => m.EventType == "CourierReleased");
    }

    [Fact]
    public async Task ESC_11_releasing_without_reason_is_rejected_with_400()
    {
        var courier = await _fixture.SeedCourierAsync(true, 0, 5);
        var deliveryId = await LogiTrackerApiFixture.CreateDeliveryAsync(_client);
        await AssignAsync(deliveryId, courier);

        var response = await _client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assignment/release", new { reason = "" });

        await AssertBusinessErrorAsync(response, HttpStatusCode.BadRequest, "release_reason_required");
    }

    [Fact]
    public async Task ESC_06_an_unknown_delivery_returns_404()
    {
        var courier = await _fixture.SeedCourierAsync(true, 0, 5);

        var response = await AssignAsync(Guid.NewGuid(), courier);

        await AssertBusinessErrorAsync(response, HttpStatusCode.NotFound, "delivery_not_found");
    }

    [Fact]
    public async Task R2_the_same_order_sent_twice_creates_a_single_delivery()
    {
        var orderId = Guid.NewGuid();

        var first = await _client.PostAsJsonAsync("/api/deliveries", LogiTrackerApiFixture.NewDeliveryRequest(orderId));
        var second = await _client.PostAsJsonAsync("/api/deliveries", LogiTrackerApiFixture.NewDeliveryRequest(orderId));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var count = await _fixture.WithDbAsync(db => db.Deliveries.CountAsync(d => d.OrderId == new Domain.Deliveries.OrderId(orderId)));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ESC_12_an_inverted_window_is_rejected_with_400()
    {
        var request = new
        {
            orderId = Guid.NewGuid(),
            address = new { street = "Calle 1", city = "Bogota", postalCode = "110111" },
            windowStart = "2026-10-31T18:00:00Z",
            windowEnd = "2026-10-31T14:00:00Z",
            packageCount = 1,
        };

        var response = await _client.PostAsJsonAsync("/api/deliveries", request);

        await AssertBusinessErrorAsync(response, HttpStatusCode.BadRequest, "invalid_delivery_window");
    }

    private Task<HttpResponseMessage> AssignAsync(Guid deliveryId, Guid courierId) =>
        _client.PostAsJsonAsync($"/api/deliveries/{deliveryId}/assignment", new { courierId });

    private async Task<JsonElement> GetDeliveryAsync(Guid deliveryId) =>
        await _client.GetFromJsonAsync<JsonElement>($"/api/deliveries/{deliveryId}");

    internal static async Task AssertBusinessErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.True(problem.TryGetProperty("correlationId", out _), "Every error must carry a correlation id.");
    }
}
