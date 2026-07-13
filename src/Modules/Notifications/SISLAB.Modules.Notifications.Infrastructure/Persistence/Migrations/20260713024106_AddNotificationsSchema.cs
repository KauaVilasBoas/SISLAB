using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Notifications.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Creates the <c>notifications</c> schema and its single <c>notifications</c> table (card #64a). The
    /// idempotency invariant "one active (unread) notification per (company, dedupe key)" is enforced by the
    /// PARTIAL unique index <c>ux_notifications_company_id_dedupe_key_active</c> filtered to
    /// <c>is_read = false</c>: the write store relies on it with <c>ON CONFLICT ... DO NOTHING</c>, and reading
    /// a notification frees its key so the same alert can re-fire in a later cycle. No outbox table lives here
    /// — under Option A a notification is the terminal effect of an alert, not an event to propagate.
    /// </summary>
    /// <inheritdoc />
    public partial class AddNotificationsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    reference_target_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    reference_target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dedupe_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    read_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_company_id_created_at_utc",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "company_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_notifications_company_id_dedupe_key_active",
                schema: "notifications",
                table: "notifications",
                columns: new[] { "company_id", "dedupe_key" },
                unique: true,
                filter: "is_read = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notifications",
                schema: "notifications");
        }
    }
}
