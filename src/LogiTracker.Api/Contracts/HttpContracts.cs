using System.ComponentModel.DataAnnotations;

namespace LogiTracker.Api.Contracts;

// Contratos HTTP. Solo validan el formato; las reglas de negocio las aplica el dominio.

public sealed record AddressRequest(
    [Required] string? Street,
    [Required] string? City,
    [Required] string? PostalCode);

/// <summary>Contrato C-01 "Pedido listo para logística". No incluye precio ni datos de pago.</summary>
public sealed record CreateDeliveryRequest(
    [Required] Guid? OrderId,
    [Required] AddressRequest? Address,
    [Required] DateTimeOffset? WindowStart,
    [Required] DateTimeOffset? WindowEnd,
    int PackageCount);

public sealed record CreateDeliveryResponse(Guid DeliveryId, bool Created);

/// <summary>El operador no se envía: se toma de la identidad autenticada.</summary>
public sealed record AssignCourierRequest([Required] Guid? CourierId);

public sealed record AssignCourierResponse(Guid DeliveryId, Guid CourierId, string Status, DateTimeOffset AssignedAt);

public sealed record ReleaseCourierRequest(string? Reason);

public sealed record ReleaseCourierResponse(Guid DeliveryId, string Status);

public sealed record DeadLetterResponse(Guid Id, string EventType, int Attempts, string? LastError, DateTimeOffset OccurredAt);
