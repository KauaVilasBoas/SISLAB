using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agenda");

            migrationBuilder.CreateTable(
                name: "bioterium_assignments",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    responsible_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    swapped_from_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    swap_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bioterium_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booked_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    activity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    has_conflict_warning = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    dead_lettered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "presentations",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    doi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    presenter_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reminder_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_presentations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bioterium_assignments_company_date",
                schema: "agenda",
                table: "bioterium_assignments",
                columns: new[] { "company_id", "assignment_date" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_company_date",
                schema: "agenda",
                table: "bookings",
                columns: new[] { "company_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_company_room_date",
                schema: "agenda",
                table: "bookings",
                columns: new[] { "company_id", "room_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_presentations_company_date",
                schema: "agenda",
                table: "presentations",
                columns: new[] { "company_id", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "ix_rooms_company_id_id",
                schema: "agenda",
                table: "rooms",
                columns: new[] { "company_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bioterium_assignments",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "presentations",
                schema: "agenda");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "agenda");
        }
    }
}
