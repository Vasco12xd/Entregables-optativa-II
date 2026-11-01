namespace LogiTracker.Infrastructure.Persistence;

/// <summary>
/// Turno, carga y capacidad de un repartidor: la fuente de disponibilidad del contrato C-02.
/// Es un modelo de persistencia, no del dominio: el repartidor está fuera del agregado Entrega (ADR-002 D-02).
/// </summary>
public sealed class CourierRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public bool IsOnShift { get; set; }
    public int ActiveDeliveries { get; set; }
    public int MaxDeliveries { get; set; }
}

/// <summary>Operador autorizado a asignar. Solo se guarda el hash de su clave de API, nunca la clave.</summary>
public sealed class OperatorRecord
{
    public Guid Id { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
