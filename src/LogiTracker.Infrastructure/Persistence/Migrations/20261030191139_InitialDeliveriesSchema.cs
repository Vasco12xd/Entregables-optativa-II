using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LogiTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDeliveriesSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "deliveries");

            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.CreateTable(
                name: "couriers",
                schema: "deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_on_shift = table.Column<bool>(type: "boolean", nullable: false),
                    active_deliveries = table.Column<int>(type: "integer", nullable: false),
                    max_deliveries = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_couriers", x => x.id);
                    table.CheckConstraint("ck_couriers_capacity", "max_deliveries > 0 AND active_deliveries >= 0 AND active_deliveries <= max_deliveries");
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                schema: "deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    address_street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    window_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    window_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliveries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operators",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    api_key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operators", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "delivery_assignments",
                schema: "deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    courier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    released_by = table.Column<Guid>(type: "uuid", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    release_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_assignments", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_assignments_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalSchema: "deliveries",
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_couriers_code",
                schema: "deliveries",
                table: "couriers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_order_id",
                schema: "deliveries",
                table: "deliveries",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_assignments_courier_id",
                schema: "deliveries",
                table: "delivery_assignments",
                column: "courier_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_assignments_delivery_id",
                schema: "deliveries",
                table: "delivery_assignments",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_operators_api_key_hash",
                schema: "access",
                table: "operators",
                column: "api_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_status_next_attempt_at",
                schema: "deliveries",
                table: "outbox_messages",
                columns: new[] { "status", "next_attempt_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "couriers",
                schema: "deliveries");

            migrationBuilder.DropTable(
                name: "delivery_assignments",
                schema: "deliveries");

            migrationBuilder.DropTable(
                name: "operators",
                schema: "access");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "deliveries");

            migrationBuilder.DropTable(
                name: "deliveries",
                schema: "deliveries");
        }
    }
}
