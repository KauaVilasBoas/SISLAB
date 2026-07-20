using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEntryReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "entry_reminders",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    minutes_before = table.Column<int>(type: "integer", nullable: false),
                    notification_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entry_reminders", x => x.id);
                    table.ForeignKey(
                        name: "fk_entry_reminder_agenda_entries_agenda_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "agenda",
                        principalTable: "agenda_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entry_reminders_entry_id",
                schema: "agenda",
                table: "entry_reminders",
                column: "entry_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entry_reminders",
                schema: "agenda");
        }
    }
}
