using LogiTracker.Domain.Common;
using LogiTracker.Domain.Deliveries;

namespace LogiTracker.Domain.Tests;

/// <summary>Datos de los escenarios de docs/actividad-3/invariantes-y-escenarios.md: entrega E-245, Ana, Luis y Marta.</summary>
internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);

    public static readonly CourierId Ana = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly CourierId Luis = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    public static readonly OperatorId Marta = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

    public static readonly CourierAvailability AvailableWithRoom = new(isOnShift: true, assignedDeliveries: 3, maxDeliveries: 5);

    public static Delivery PendingDelivery() =>
        Delivery.CreateFromOrder(
            DeliveryId.New(),
            new OrderId(Guid.NewGuid()),
            new DeliveryAddress("Calle 10 # 20-30", "Bogota", "110111"),
            new DeliveryWindow(Now.AddHours(1), Now.AddHours(5)),
            packageCount: 2,
            Now);

    public static Delivery DeliveryAssignedTo(CourierId courier)
    {
        var delivery = PendingDelivery();
        delivery.AssignCourier(courier, AvailableWithRoom, Marta, Now);
        delivery.ClearDomainEvents();
        return delivery;
    }

    public static string ViolationCode(Action action) =>
        Assert.Throws<DomainRuleViolationException>(action).Code;
}
