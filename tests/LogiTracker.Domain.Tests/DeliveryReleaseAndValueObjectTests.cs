using LogiTracker.Domain.Deliveries;
using static LogiTracker.Domain.Tests.TestData;

namespace LogiTracker.Domain.Tests;

public sealed class DeliveryReleaseTests
{
    [Fact]
    public void ESC_10_releasing_with_a_reason_returns_delivery_to_pending_and_keeps_history()
    {
        var delivery = DeliveryAssignedTo(Ana);
        var later = Now.AddMinutes(30);

        delivery.ReleaseCourier(new ReleaseReason("Vehiculo averiado"), Marta, later);

        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.ActiveAssignment);
        var released = Assert.Single(delivery.History);
        Assert.Equal(later, released.ReleasedAt);
        Assert.Equal("Vehiculo averiado", released.ReleaseReason!.Value);
        Assert.Equal(Marta, released.ReleasedBy);
        Assert.IsType<CourierReleased>(Assert.Single(delivery.DomainEvents));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ESC_11_a_release_reason_cannot_be_empty(string reason)
    {
        var code = ViolationCode(() => _ = new ReleaseReason(reason));

        Assert.Equal(DeliveryErrors.ReleaseReasonRequired, code);         // INV-07
    }

    [Fact]
    public void A_pending_delivery_has_no_courier_to_release()
    {
        var delivery = PendingDelivery();

        var code = ViolationCode(() => delivery.ReleaseCourier(new ReleaseReason("Motivo"), Marta, Now));

        Assert.Equal(DeliveryErrors.DeliveryNotReleasable, code);
    }

    [Fact]
    public void INV_09_a_courier_in_transit_cannot_be_released_through_this_flow()
    {
        var delivery = DeliveryAssignedTo(Ana);
        delivery.MarkInTransit();

        var code = ViolationCode(() => delivery.ReleaseCourier(new ReleaseReason("Motivo"), Marta, Now));

        Assert.Equal(DeliveryErrors.DeliveryNotReleasable, code);
        Assert.Equal(Ana, delivery.ActiveAssignment!.CourierId);
    }

    [Fact]
    public void After_a_release_the_delivery_can_be_assigned_to_another_courier()
    {
        var delivery = DeliveryAssignedTo(Ana);
        delivery.ReleaseCourier(new ReleaseReason("Reasignacion por zona"), Marta, Now);

        delivery.AssignCourier(Luis, AvailableWithRoom, Marta, Now.AddMinutes(1));

        Assert.Equal(Luis, delivery.ActiveAssignment!.CourierId);
        Assert.Equal(2, delivery.History.Count);
    }
}

public sealed class ValueObjectTests
{
    [Fact]
    public void ESC_12_an_inverted_delivery_window_is_rejected()
    {
        var start = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 31, 14, 0, 0, TimeSpan.Zero);

        var code = ViolationCode(() => _ = new DeliveryWindow(start, end));

        Assert.Equal(DeliveryErrors.InvalidDeliveryWindow, code);         // INV-08
    }

    [Fact]
    public void A_delivery_window_with_no_duration_is_rejected()
    {
        var code = ViolationCode(() => _ = new DeliveryWindow(Now, Now));

        Assert.Equal(DeliveryErrors.InvalidDeliveryWindow, code);
    }

    [Theory]
    [InlineData("", "Bogota", "110111")]
    [InlineData("Calle 1", " ", "110111")]
    [InlineData("Calle 1", "Bogota", "")]
    public void An_incomplete_address_is_rejected(string street, string city, string postalCode)
    {
        var code = ViolationCode(() => _ = new DeliveryAddress(street, city, postalCode));

        Assert.Equal(DeliveryErrors.InvalidAddress, code);
    }

    [Fact]
    public void Value_objects_are_equal_by_their_data()
    {
        Assert.Equal(new DeliveryAddress("Calle 1", "Bogota", "110111"), new DeliveryAddress(" Calle 1 ", "Bogota", "110111"));
        Assert.Equal(new CourierId(Ana.Value), Ana);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, 0)]
    public void An_inconsistent_courier_availability_is_rejected(int assigned, int max)
    {
        var code = ViolationCode(() => _ = new CourierAvailability(true, assigned, max));

        Assert.Equal(DeliveryErrors.InvalidCourierAvailability, code);
    }

    [Fact]
    public void Empty_identifiers_are_rejected()
    {
        Assert.Equal(DeliveryErrors.InvalidIdentifier, ViolationCode(() => _ = new DeliveryId(Guid.Empty)));
        Assert.Equal(DeliveryErrors.InvalidIdentifier, ViolationCode(() => _ = new CourierId(Guid.Empty)));
    }

    [Fact]
    public void A_delivery_needs_at_least_one_package()
    {
        var code = ViolationCode(() => Delivery.CreateFromOrder(
            DeliveryId.New(),
            new OrderId(Guid.NewGuid()),
            new DeliveryAddress("Calle 1", "Bogota", "110111"),
            new DeliveryWindow(Now, Now.AddHours(1)),
            packageCount: 0,
            Now));

        Assert.Equal(DeliveryErrors.InvalidPackageCount, code);
    }
}
