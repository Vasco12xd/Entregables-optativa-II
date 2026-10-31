using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>Motivo por el que se libera a un repartidor. INV-07: no puede estar vacío.</summary>
public sealed record ReleaseReason
{
    public const int MaxLength = 200;

    public ReleaseReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
            throw new DomainRuleViolationException(DeliveryErrors.ReleaseReasonRequired);

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
