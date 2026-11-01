using LogiTracker.Application.Deliveries;
using LogiTracker.Application.Deliveries.CreateDelivery;
using LogiTracker.Application.Deliveries.ReleaseCourier;
using LogiTracker.Application.Notifications;
using LogiTracker.Application.Outbox;
using LogiTracker.Domain.Deliveries;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogiTracker.Application.Tests;

public sealed class CreateAndReleaseUseCaseTests
{
    private static readonly Guid Marta = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly InMemoryDeliveryRepository _repository = new();
    private readonly RecordingEventPublisher _publisher = new();
    private readonly FixedClock _clock = new();

    [Fact]
    public async Task Creating_the_same_order_twice_returns_the_existing_delivery()
    {
        var useCase = new CreateDeliveryUseCase(_repository, _clock, NullLogger<CreateDeliveryUseCase>.Instance);
        var command = ValidCommand(Guid.NewGuid());

        var first = await useCase.ExecuteAsync(command, CancellationToken.None);
        var second = await useCase.ExecuteAsync(command, CancellationToken.None);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.DeliveryId, second.DeliveryId);
    }

    [Fact]
    public async Task ESC_12_an_inverted_window_is_rejected_with_its_business_code()
    {
        var useCase = new CreateDeliveryUseCase(_repository, _clock, NullLogger<CreateDeliveryUseCase>.Instance);
        var command = ValidCommand(Guid.NewGuid()) with { WindowStart = _clock.UtcNow.AddHours(5), WindowEnd = _clock.UtcNow.AddHours(1) };

        var result = await useCase.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(DeliveryErrors.InvalidDeliveryWindow, result.ErrorCode);
    }

    [Fact]
    public async Task ESC_10_release_with_reason_returns_delivery_to_pending_and_publishes_the_fact()
    {
        var deliveryId = await CreateAssignedDelivery();
        var useCase = new ReleaseCourierUseCase(_repository, _publisher, _clock, NullLogger<ReleaseCourierUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new ReleaseCourierCommand(deliveryId, "Vehiculo averiado", Marta), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(nameof(DeliveryStatus.PendingAssignment), result.Status);
        Assert.IsType<CourierReleased>(Assert.Single(_publisher.Published));
    }

    [Fact]
    public async Task ESC_11_release_without_reason_is_rejected()
    {
        var deliveryId = await CreateAssignedDelivery();
        var useCase = new ReleaseCourierUseCase(_repository, _publisher, _clock, NullLogger<ReleaseCourierUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new ReleaseCourierCommand(deliveryId, "  ", Marta), CancellationToken.None);

        Assert.Equal(DeliveryErrors.ReleaseReasonRequired, result.ErrorCode);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Releasing_an_unknown_delivery_reports_not_found()
    {
        var useCase = new ReleaseCourierUseCase(_repository, _publisher, _clock, NullLogger<ReleaseCourierUseCase>.Instance);

        var result = await useCase.ExecuteAsync(new ReleaseCourierCommand(Guid.NewGuid(), "Motivo", Marta), CancellationToken.None);

        Assert.Equal(ApplicationErrors.DeliveryNotFound, result.ErrorCode);
    }

    private CreateDeliveryCommand ValidCommand(Guid orderId) =>
        new(orderId, "Calle 10 # 20-30", "Bogota", "110111", _clock.UtcNow.AddHours(1), _clock.UtcNow.AddHours(5), 2);

    private async Task<Guid> CreateAssignedDelivery()
    {
        var created = await new CreateDeliveryUseCase(_repository, _clock, NullLogger<CreateDeliveryUseCase>.Instance)
            .ExecuteAsync(ValidCommand(Guid.NewGuid()), CancellationToken.None);
        var delivery = await _repository.GetAsync(new DeliveryId(created.DeliveryId!.Value), CancellationToken.None);
        delivery!.AssignCourier(new CourierId(Guid.NewGuid()), new CourierAvailability(true, 0, 5), new OperatorId(Marta), _clock.UtcNow);
        delivery.ClearDomainEvents();
        return delivery.Id.Value;
    }
}

/// <summary>Reintentos limitados, espera creciente y Dead Letter Queue (video 21).</summary>
public sealed class DispatchOutboxUseCaseTests
{
    private readonly InMemoryOutboxStore _store = new();
    private readonly ScriptedNotificationSender _sender = new();
    private readonly FixedClock _clock = new();
    private readonly OutboxPolicy _policy = new() { MaxAttempts = 3, BaseRetryDelay = TimeSpan.FromSeconds(2) };

    private DispatchOutboxUseCase CreateUseCase() =>
        new(_store, _sender, _clock, _policy, NullLogger<DispatchOutboxUseCase>.Instance);

    [Fact]
    public async Task A_successful_send_marks_the_message_as_processed()
    {
        var message = _store.Add();

        var summary = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, summary.Dispatched);
        Assert.Equal(OutboxStatus.Processed, message.Status);
        Assert.Contains(message.Id, _sender.Sent);
    }

    [Fact]
    public async Task A_transient_failure_schedules_a_retry_with_backoff_and_keeps_the_error()
    {
        var message = _store.Add();
        _sender.FailWith = new NotificationTransientException("Provider timeout");

        var summary = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, summary.RetriesScheduled);
        Assert.Equal(OutboxStatus.Pending, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal(_clock.UtcNow.AddSeconds(2), message.NextAttemptAt);
        Assert.Contains("Provider timeout", message.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exhausting_the_retries_moves_the_message_to_dead_letters()
    {
        var message = _store.Add(attempts: 2);   // Este será el tercer y último intento
        _sender.FailWith = new NotificationTransientException("Provider timeout");

        var summary = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(1, summary.DeadLettered);
        Assert.Equal(OutboxStatus.DeadLettered, message.Status);
        Assert.Equal(3, message.Attempts);
    }

    [Fact]
    public async Task A_permanent_failure_goes_to_dead_letters_without_retrying()
    {
        var message = _store.Add();
        _sender.FailWith = new NotificationPermanentException("Rejected format");

        await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(OutboxStatus.DeadLettered, message.Status);
        Assert.Equal(1, message.Attempts);
    }

    [Fact]
    public async Task A_message_waiting_for_its_retry_time_is_not_sent_early()
    {
        var message = _store.Add();
        message.NextAttemptAt = _clock.UtcNow.AddMinutes(1);

        var summary = await CreateUseCase().ExecuteAsync(CancellationToken.None);

        Assert.Equal(0, summary.Dispatched);
        Assert.Empty(_sender.Sent);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(20, 300)]
    public void Retry_delay_grows_exponentially_up_to_a_maximum(int attempt, int expectedSeconds)
    {
        var policy = new OutboxPolicy { BaseRetryDelay = TimeSpan.FromSeconds(2), MaxRetryDelay = TimeSpan.FromMinutes(5) };

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), policy.RetryDelayFor(attempt));
    }
}
