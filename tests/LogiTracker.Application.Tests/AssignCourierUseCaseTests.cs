using LogiTracker.Application.Deliveries;
using LogiTracker.Application.Deliveries.AssignCourier;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Domain.Deliveries;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogiTracker.Application.Tests;

/// <summary>
/// Pruebas del caso de uso con adaptadores en memoria: verifican la coordinación (pasos 1 a 6)
/// y la traducción de fallas de los puertos a códigos de negocio.
/// </summary>
public sealed class AssignCourierUseCaseTests
{
    private static readonly CourierId Ana = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly CourierId Luis = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly OperatorId Marta = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    private readonly InMemoryDeliveryRepository _repository = new();
    private readonly FakeCourierAvailability _availability = new();
    private readonly RecordingEventPublisher _publisher = new();
    private readonly FixedClock _clock = new();
    private readonly AssignCourierUseCase _useCase;

    public AssignCourierUseCaseTests()
    {
        _useCase = new AssignCourierUseCase(_repository, _availability, _publisher, _clock, NullLogger<AssignCourierUseCase>.Instance);
        _availability.Set(Ana, new CourierAvailability(true, 3, 5));
        _availability.Set(Luis, new CourierAvailability(true, 1, 5));
    }

    [Fact]
    public async Task ESC_01_assigns_saves_and_publishes_the_fact()
    {
        var delivery = StorePendingDelivery();

        var result = await Assign(delivery, Ana);

        Assert.True(result.Succeeded);
        Assert.Equal(Ana.Value, result.CourierId);
        Assert.Equal(nameof(DeliveryStatus.Assigned), result.Status);
        Assert.Equal(_clock.UtcNow, result.AssignedAt);
        Assert.Equal(1, _repository.SaveCount);
        Assert.IsType<CourierAssigned>(Assert.Single(_publisher.Published));
        Assert.Empty(delivery.DomainEvents);   // Los hechos se limpian tras confirmarse
    }

    [Fact]
    public async Task ESC_02_does_not_query_availability_when_another_courier_is_active()
    {
        var delivery = StorePendingDelivery();
        await Assign(delivery, Luis);
        var callsBefore = _availability.Calls;

        var result = await Assign(delivery, Ana);

        Assert.Equal(DeliveryErrors.DeliveryAlreadyAssigned, result.ErrorCode);
        Assert.Equal(callsBefore, _availability.Calls);
    }

    [Fact]
    public async Task ESC_03_a_retry_with_the_same_courier_succeeds_without_saving_or_publishing_again()
    {
        var delivery = StorePendingDelivery();
        await Assign(delivery, Ana);
        _availability.SourceDown = true;   // Incluso con la fuente caída, el reintento responde con éxito

        var result = await Assign(delivery, Ana);

        Assert.True(result.Succeeded);
        Assert.Equal(1, _repository.SaveCount);
        Assert.Single(_publisher.Published);
    }

    [Fact]
    public async Task ESC_06_reports_an_unknown_delivery()
    {
        var result = await _useCase.ExecuteAsync(new AssignCourierCommand(Guid.NewGuid(), Ana.Value, Marta.Value), CancellationToken.None);

        Assert.Equal(ApplicationErrors.DeliveryNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Reports_an_unknown_courier()
    {
        var delivery = StorePendingDelivery();

        var result = await Assign(delivery, new CourierId(Guid.NewGuid()));

        Assert.Equal(ApplicationErrors.CourierNotFound, result.ErrorCode);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
    }

    [Fact]
    public async Task ESC_08_translates_a_concurrency_conflict_into_a_business_code()
    {
        var delivery = StorePendingDelivery();
        _repository.FailNextSaveWith = new ConcurrencyConflictException();

        var result = await Assign(delivery, Ana);

        Assert.False(result.Succeeded);
        Assert.Equal(ApplicationErrors.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task ESC_09_availability_outage_leaves_the_delivery_pending_and_reports_a_temporary_error()
    {
        var delivery = StorePendingDelivery();
        _availability.SourceDown = true;

        var result = await Assign(delivery, Ana);

        Assert.Equal(ApplicationErrors.AvailabilityServiceUnavailable, result.ErrorCode);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Equal(0, _repository.SaveCount);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task ESC_05_capacity_lost_between_query_and_save_is_reported_as_capacity_reached()
    {
        // La disponibilidad decía "hay cupo", pero otra asignación lo tomó antes del guardado (guarda atómica, ADR-003).
        var delivery = StorePendingDelivery();
        _repository.FailNextSaveWith = new CourierCapacityExceededException();

        var result = await Assign(delivery, Ana);

        Assert.Equal(DeliveryErrors.CourierCapacityReached, result.ErrorCode);
    }

    [Fact]
    public async Task Rejects_an_empty_courier_identifier_without_touching_the_ports()
    {
        var delivery = StorePendingDelivery();

        var result = await _useCase.ExecuteAsync(new AssignCourierCommand(delivery.Id.Value, Guid.Empty, Marta.Value), CancellationToken.None);

        Assert.Equal(DeliveryErrors.InvalidIdentifier, result.ErrorCode);
        Assert.Equal(0, _availability.Calls);
    }

    private Delivery StorePendingDelivery()
    {
        var delivery = Delivery.CreateFromOrder(
            DeliveryId.New(),
            new OrderId(Guid.NewGuid()),
            new DeliveryAddress("Calle 10 # 20-30", "Bogota", "110111"),
            new DeliveryWindow(_clock.UtcNow.AddHours(1), _clock.UtcNow.AddHours(5)),
            packageCount: 1,
            _clock.UtcNow);
        _repository.Store(delivery);
        return delivery;
    }

    private Task<AssignCourierResult> Assign(Delivery delivery, CourierId courier) =>
        _useCase.ExecuteAsync(new AssignCourierCommand(delivery.Id.Value, courier.Value, Marta.Value), CancellationToken.None);
}
