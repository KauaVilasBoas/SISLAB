using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDeadLetter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "inventory",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_processed_at_utc",
                schema: "inventory",
                table: "outbox_messages");

            migrationBuilder.AddColumn<int>(
                name: "attempt_count",
                schema: "inventory",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "dead_lettered_at_utc",
                schema: "inventory",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "inventory",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_pending",
                schema: "inventory",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "attempt_count",
                schema: "inventory",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "dead_lettered_at_utc",
                schema: "inventory",
                table: "outbox_messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_occurred_on_utc",
                schema: "inventory",
                table: "outbox_messages",
                column: "occurred_on_utc");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at_utc",
                schema: "inventory",
                table: "outbox_messages",
                column: "processed_at_utc");
        }
    }
}
