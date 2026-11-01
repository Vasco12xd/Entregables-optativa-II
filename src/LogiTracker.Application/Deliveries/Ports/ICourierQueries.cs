namespace LogiTracker.Application.Deliveries.Ports;

/// <summary>Consulta de solo lectura para que el operador vea los repartidores y su carga actual.</summary>
public interface ICourierQueries
{
    Task<IReadOnlyList<CourierSummary>> ListAsync(CancellationToken cancellationToken);
}

public sealed record CourierSummary(
    Guid Id,
    string Code,
    string Alias,
    bool IsOnShift,
    int ActiveDeliveries,
    int MaxDeliveries);
