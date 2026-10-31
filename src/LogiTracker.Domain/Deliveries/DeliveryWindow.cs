using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>Intervalo en el que el cliente espera recibir el paquete. INV-08: el inicio es anterior al fin.</summary>
public sealed record DeliveryWindow
{
    public DeliveryWindow(DateTimeOffset start, DateTimeOffset end)
    {
        if (start >= end)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidDeliveryWindow);

        Start = start;
        End = end;
    }

    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
}
