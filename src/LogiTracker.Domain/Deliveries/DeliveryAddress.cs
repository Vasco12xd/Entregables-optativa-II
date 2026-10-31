using LogiTracker.Domain.Common;

namespace LogiTracker.Domain.Deliveries;

/// <summary>Lugar al que se lleva el paquete. Los tres campos son obligatorios.</summary>
public sealed record DeliveryAddress
{
    public DeliveryAddress(string street, string city, string postalCode)
    {
        if (string.IsNullOrWhiteSpace(street) || street.Length > 200
            || string.IsNullOrWhiteSpace(city) || city.Length > 100
            || string.IsNullOrWhiteSpace(postalCode) || postalCode.Length > 20)
        {
            throw new DomainRuleViolationException(DeliveryErrors.InvalidAddress);
        }

        Street = street.Trim();
        City = city.Trim();
        PostalCode = postalCode.Trim();
    }

    public string Street { get; }
    public string City { get; }
    public string PostalCode { get; }
}
