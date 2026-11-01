using LogiTracker.Application.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LogiTracker.Infrastructure.Persistence.Configurations;

internal sealed class CourierRecordConfiguration : IEntityTypeConfiguration<CourierRecord>
{
    public void Configure(EntityTypeBuilder<CourierRecord> builder)
    {
        builder.ToTable("couriers", LogiTrackerDbContext.DeliveriesSchema, table =>
        {
            // Respaldo en la base de las reglas de capacidad: la carga nunca es negativa ni supera la capacidad.
            table.HasCheckConstraint("ck_couriers_capacity", "max_deliveries > 0 AND active_deliveries >= 0 AND active_deliveries <= max_deliveries");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Code).HasMaxLength(20);
        builder.HasIndex(c => c.Code).IsUnique();
        builder.Property(c => c.Alias).HasMaxLength(50);
    }
}

internal sealed class OperatorRecordConfiguration : IEntityTypeConfiguration<OperatorRecord>
{
    public void Configure(EntityTypeBuilder<OperatorRecord> builder)
    {
        builder.ToTable("operators", LogiTrackerDbContext.AccessSchema);

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.Alias).HasMaxLength(50);
        builder.Property(o => o.ApiKeyHash).HasMaxLength(64).IsFixedLength();
        builder.HasIndex(o => o.ApiKeyHash).IsUnique();
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", LogiTrackerDbContext.DeliveriesSchema);

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.EventType).HasMaxLength(100);
        builder.Property(m => m.Payload).HasColumnType("jsonb");
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.LastError).HasMaxLength(500);

        // Búsqueda eficiente de mensajes pendientes por el despachador.
        builder.HasIndex(m => new { m.Status, m.NextAttemptAt });
    }
}
