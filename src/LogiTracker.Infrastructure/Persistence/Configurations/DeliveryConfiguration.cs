using LogiTracker.Domain.Deliveries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogiTracker.Infrastructure.Persistence.Configurations;

/// <summary>
/// Traduce el agregado Entrega a tablas. El dominio no conoce este mapeo:
/// la dependencia va de infraestructura hacia el dominio, nunca al revés.
/// </summary>
internal sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public const string VersionProperty = "Version";
    private const string DeliveryIdForeignKey = "DeliveryId";

    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries", LogiTrackerDbContext.DeliveriesSchema);

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new DeliveryId(value))
            .ValueGeneratedNever();

        builder.Property(d => d.OrderId)
            .HasConversion(id => id.Value, value => new OrderId(value));
        // Una entrega por pedido: protege contra pedidos reenviados por un canal (riesgo R2).
        builder.HasIndex(d => d.OrderId).IsUnique();

        builder.ComplexProperty(d => d.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(100);
            address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20);
        });

        builder.ComplexProperty(d => d.Window, window =>
        {
            window.Property(w => w.Start).HasColumnName("window_start");
            window.Property(w => w.End).HasColumnName("window_end");
        });

        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(d => d.PackageCount);
        builder.Property(d => d.CreatedAt);

        // Control optimista de concurrencia (ADR-002 D-04). Es un detalle de persistencia, por eso no está en el dominio.
        builder.Property<int>(VersionProperty).IsConcurrencyToken();

        builder.Ignore(d => d.ActiveAssignment);
        builder.Ignore(d => d.DomainEvents);

        builder.HasMany(d => d.History)
            .WithOne()
            .HasForeignKey(DeliveryIdForeignKey)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(d => d.History)
            .HasField("_history")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CourierAssignmentConfiguration : IEntityTypeConfiguration<CourierAssignment>
{
    public void Configure(EntityTypeBuilder<CourierAssignment> builder)
    {
        builder.ToTable("delivery_assignments", LogiTrackerDbContext.DeliveriesSchema);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property<DeliveryId>("DeliveryId")
            .HasConversion(id => id.Value, value => new DeliveryId(value));

        builder.Property(a => a.CourierId)
            .HasConversion(id => id.Value, value => new CourierId(value));
        builder.Property(a => a.AssignedBy)
            .HasConversion(id => id.Value, value => new OperatorId(value));
        builder.Property(a => a.ReleasedBy)
            .HasConversion(id => id!.Value, value => new OperatorId(value));
        builder.Property(a => a.ReleaseReason)
            .HasConversion(reason => reason!.Value, value => new ReleaseReason(value))
            .HasMaxLength(ReleaseReason.MaxLength);

        builder.Ignore(a => a.IsActive);
        builder.HasIndex(a => a.CourierId);
    }
}
