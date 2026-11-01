namespace LogiTracker.Application.Common;

/// <summary>Puerto de tiempo: permite probar las reglas con una hora controlada (INV-05).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
