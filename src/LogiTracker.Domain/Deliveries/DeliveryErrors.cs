namespace LogiTracker.Domain.Deliveries;

/// <summary>Catálogo de códigos de error del contexto de Entregas (docs/actividad-3/casos-uso-puertos-adaptadores.md).</summary>
public static class DeliveryErrors
{
    // Reglas de asignación y liberación
    public const string DeliveryAlreadyAssigned = "delivery_already_assigned";   // INV-01
    public const string DeliveryNotAssignable = "delivery_not_assignable";       // INV-02, INV-09
    public const string CourierUnavailable = "courier_unavailable";              // INV-03
    public const string CourierCapacityReached = "courier_capacity_reached";     // INV-04
    public const string DeliveryNotReleasable = "delivery_not_releasable";       // INV-09
    public const string ReleaseReasonRequired = "release_reason_required";       // INV-07

    // Validación de objetos de valor
    public const string InvalidIdentifier = "invalid_identifier";
    public const string InvalidAddress = "invalid_address";
    public const string InvalidDeliveryWindow = "invalid_delivery_window";       // INV-08
    public const string InvalidPackageCount = "invalid_package_count";
    public const string InvalidCourierAvailability = "invalid_courier_availability";
}
