namespace LogiTracker.Domain.Common;

/// <summary>Hecho del dominio: algo que ya ocurrió y que otras partes pueden necesitar saber.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
