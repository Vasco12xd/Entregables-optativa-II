using LogiTracker.Application.Common;
using LogiTracker.Application.Deliveries.Ports;
using LogiTracker.Application.Notifications;
using LogiTracker.Application.Outbox;
using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;

namespace LogiTracker.Application.Tests;

// Adaptadores de prueba: implementan los mismos puertos que los adaptadores reales, en memoria.
// Que el caso de uso funcione con ellos demuestra que no depende de la infraestructura concreta.

internal sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly Dictionary<Guid, Delivery> _deliveries = [];

    public Exception? FailNextSaveWith { get; set; }
    public int SaveCount { get; private set; }

    public void Store(Delivery delivery) => _deliveries[delivery.Id.Value] = delivery;

    public Task<Delivery?> GetAsync(DeliveryId id, CancellationToken cancellationToken) =>
        Task.FromResult(_deliveries.GetValueOrDefault(id.Value));

    public Task<Delivery?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_deliveries.Values.SingleOrDefault(d => d.OrderId == orderId));

    public Task AddAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        if (_deliveries.Values.Any(d => d.OrderId == delivery.OrderId))
            throw new DuplicateOrderException();
        Store(delivery);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Delivery delivery, CancellationToken cancellationToken)
    {
        if (FailNextSaveWith is { } failure)
        {
            FailNextSaveWith = null;
            throw failure;
        }

        SaveCount++;
        Store(delivery);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCourierAvailability : ICourierAvailability
{
    private readonly Dictionary<Guid, CourierAvailability> _couriers = [];

    public bool SourceDown { get; set; }
    public int Calls { get; private set; }

    public void Set(CourierId courierId, CourierAvailability availability) => _couriers[courierId.Value] = availability;

    public Task<CourierAvailability?> GetAsync(CourierId courierId, CancellationToken cancellationToken)
    {
        Calls++;
        if (SourceDown)
            throw new AvailabilityUnavailableException("Simulated outage.");
        return Task.FromResult(_couriers.GetValueOrDefault(courierId.Value));
    }
}

internal sealed class RecordingEventPublisher : IDeliveryEventPublisher
{
    public List<IDomainEvent> Published { get; } = [];

    public Task PublishAsync(IReadOnlyCollection<IDomainEvent> events, CancellationToken cancellationToken)
    {
        Published.AddRange(events);
        return Task.CompletedTask;
    }
}

internal sealed class FixedClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);
}

internal sealed class InMemoryOutboxStore : IOutboxStore
{
    public List<OutboxMessage> Messages { get; } = [];

    public OutboxMessage Add(string eventType = "CourierAssigned", int attempts = 0)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = "{}",
            OccurredAt = DateTimeOffset.UnixEpoch,
            Status = OutboxStatus.Pending,
            Attempts = attempts,
        };
        Messages.Add(message);
        return message;
    }

    public Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(int batchSize, DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken)
    {
        var due = Messages.Where(m => m.Status == OutboxStatus.Pending && m.NextAttemptAt <= now).Take(batchSize).ToList();
        due.ForEach(m => m.Status = OutboxStatus.Processing);
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(due);
    }

    public Task MarkProcessedAsync(Guid messageId, DateTimeOffset processedAt, CancellationToken cancellationToken)
    {
        var message = Find(messageId);
        message.Status = OutboxStatus.Processed;
        message.ProcessedAt = processedAt;
        return Task.CompletedTask;
    }

    public Task ScheduleRetryAsync(Guid messageId, int attempts, DateTimeOffset nextAttemptAt, string lastError, CancellationToken cancellationToken)
    {
        var message = Find(messageId);
        message.Status = OutboxStatus.Pending;
        message.Attempts = attempts;
        message.NextAttemptAt = nextAttemptAt;
        message.LastError = lastError;
        return Task.CompletedTask;
    }

    public Task MarkDeadLetteredAsync(Guid messageId, int attempts, string lastError, CancellationToken cancellationToken)
    {
        var message = Find(messageId);
        message.Status = OutboxStatus.DeadLettered;
        message.Attempts = attempts;
        message.LastError = lastError;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> ListDeadLettersAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OutboxMessage>>(Messages.Where(m => m.Status == OutboxStatus.DeadLettered).Take(limit).ToList());

    public Task<bool> RequeueAsync(Guid messageId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var message = Find(messageId);
        if (message.Status != OutboxStatus.DeadLettered)
            return Task.FromResult(false);
        message.Status = OutboxStatus.Pending;
        message.Attempts = 0;
        message.NextAttemptAt = now;
        return Task.FromResult(true);
    }

    private OutboxMessage Find(Guid id) => Messages.Single(m => m.Id == id);
}

internal sealed class ScriptedNotificationSender : INotificationSender
{
    public Exception? FailWith { get; set; }
    public List<Guid> Sent { get; } = [];

    public Task SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        if (FailWith is not null)
            throw FailWith;
        Sent.Add(request.MessageId);
        return Task.CompletedTask;
    }
}
