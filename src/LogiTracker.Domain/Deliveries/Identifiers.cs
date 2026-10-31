using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>Identidad de una entrega.</summary>
public sealed record DeliveryId
{
    public DeliveryId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidIdentifier);
        Value = value;
    }

    public Guid Value { get; }

    public static DeliveryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}

/// <summary>Identidad de un repartidor. La entrega solo lo referencia; no carga al repartidor.</summary>
public sealed record CourierId
{
    public CourierId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidIdentifier);
        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();
}

/// <summary>Referencia al pedido del contexto de Pedidos que originó la entrega.</summary>
public sealed record OrderId
{
    public OrderId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidIdentifier);
        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();
}

/// <summary>Operador que decide una asignación o liberación (trazabilidad, INV-05).</summary>
public sealed record OperatorId
{
    public OperatorId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainRuleViolationException(DeliveryErrors.InvalidIdentifier);
        Value = value;
    }

    public Guid Value { get; }

    public override string ToString() => Value.ToString();
}
