namespace LogiTracker.Application.Deliveries;

/// <summary>Códigos de error que no nacen de una invariante del dominio sino de la coordinación del caso de uso.</summary>
public static class ApplicationErrors
{
    public const string DeliveryNotFound = "delivery_not_found";
    public const string CourierNotFound = "courier_not_found";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string AvailabilityServiceUnavailable = "availability_service_unavailable";
}
