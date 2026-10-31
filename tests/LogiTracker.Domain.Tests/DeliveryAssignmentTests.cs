using LogiTracker.Domain.Deliveries;
using static LogiTracker.Domain.Tests.TestData;

namespace LogiTracker.Domain.Tests;

/// <summary>
/// Pruebas unitarias de las invariantes de asignación. No usan base de datos, red ni framework:
/// demuestran que las reglas críticas viven en el dominio.
/// </summary>
public sealed class DeliveryAssignmentTests
{
    [Fact]
    public void ESC_01_assigns_available_courier_to_pending_delivery()
    {
        // Dado que la entrega está pendiente y Ana está en turno con 3 de 5 entregas
        var delivery = PendingDelivery();

        // Cuando Marta asigna a Ana
        delivery.AssignCourier(Ana, AvailableWithRoom, Marta, Now);

        // Entonces queda asignada, con registro de quién y cuándo, y se genera el hecho
        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Equal(Ana, delivery.ActiveAssignment!.CourierId);
        Assert.Equal(Marta, delivery.ActiveAssignment.AssignedBy);       // INV-05
        Assert.Equal(Now, delivery.ActiveAssignment.AssignedAt);
        var assigned = Assert.IsType<CourierAssigned>(Assert.Single(delivery.DomainEvents));
        Assert.Equal(Ana, assigned.CourierId);
    }

    [Fact]
    public void ESC_02_rejects_assignment_when_delivery_has_another_courier()
    {
        var delivery = DeliveryAssignedTo(Luis);

        var code = ViolationCode(() => delivery.AssignCourier(Ana, AvailableWithRoom, Marta, Now));

        Assert.Equal(DeliveryErrors.DeliveryAlreadyAssigned, code);      // INV-01
        Assert.Equal(Luis, delivery.ActiveAssignment!.CourierId);
        Assert.Empty(delivery.DomainEvents);
    }

    [Fact]
    public void ESC_03_repeating_the_same_assignment_does_not_duplicate_it()
    {
        var delivery = DeliveryAssignedTo(Ana);

        delivery.AssignCourier(Ana, AvailableWithRoom, Marta, Now.AddMinutes(1));

        Assert.Single(delivery.History);                                  // INV-10
        Assert.Empty(delivery.DomainEvents);
        Assert.Equal(Now, delivery.ActiveAssignment!.AssignedAt);
    }

    [Fact]
    public void ESC_04_rejects_courier_who_is_off_shift()
    {
        var delivery = PendingDelivery();
        var offShift = new CourierAvailability(isOnShift: false, assignedDeliveries: 0, maxDeliveries: 5);

        var code = ViolationCode(() => delivery.AssignCourier(Ana, offShift, Marta, Now));

        Assert.Equal(DeliveryErrors.CourierUnavailable, code);            // INV-03
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(6, 5)]
    public void ESC_05_rejects_assignment_when_courier_route_is_saturated(int assigned, int max)
    {
        var delivery = PendingDelivery();
        var saturated = new CourierAvailability(isOnShift: true, assignedDeliveries: assigned, maxDeliveries: max);

        var code = ViolationCode(() => delivery.AssignCourier(Ana, saturated, Marta, Now));

        Assert.Equal(DeliveryErrors.CourierCapacityReached, code);        // INV-04
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.ActiveAssignment);
    }

    [Fact]
    public void ESC_05_boundary_accepts_assignment_when_one_slot_is_left()
    {
        var delivery = PendingDelivery();
        var lastSlot = new CourierAvailability(isOnShift: true, assignedDeliveries: 4, maxDeliveries: 5);

        delivery.AssignCourier(Ana, lastSlot, Marta, Now);

        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
    }

    [Fact]
    public void ESC_07_rejects_assignment_of_a_delivery_already_in_transit()
    {
        var delivery = DeliveryAssignedTo(Luis);
        delivery.MarkInTransit();

        var code = ViolationCode(() => delivery.AssignCourier(Ana, AvailableWithRoom, Marta, Now));

        Assert.Equal(DeliveryErrors.DeliveryNotAssignable, code);         // INV-02, INV-09
        Assert.Equal(Luis, delivery.ActiveAssignment!.CourierId);
    }

    [Fact]
    public void INV_06_status_and_active_courier_are_always_coherent()
    {
        var delivery = PendingDelivery();
        Assert.Null(delivery.ActiveAssignment);

        delivery.AssignCourier(Ana, AvailableWithRoom, Marta, Now);
        Assert.NotNull(delivery.ActiveAssignment);

        delivery.ReleaseCourier(new ReleaseReason("Vehiculo averiado"), Marta, Now);
        Assert.Equal(DeliveryStatus.PendingAssignment, delivery.Status);
        Assert.Null(delivery.ActiveAssignment);
    }

    [Fact]
    public void GetAssignmentRejection_answers_without_changing_the_delivery()
    {
        var delivery = DeliveryAssignedTo(Luis);

        Assert.Equal(DeliveryErrors.DeliveryAlreadyAssigned, delivery.GetAssignmentRejection(Ana));
        Assert.Null(delivery.GetAssignmentRejection(Luis));
        Assert.Single(delivery.History);
    }
}
