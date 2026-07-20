using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgendaEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agenda_entries",
                schema: "agenda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    start_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_all_day = table.Column<bool>(type: "boolean", nullable: false),
                    activity_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    experiment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recurrence_rule = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    responsible_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    excluded_dates = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agenda_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_entries_company_activity",
                schema: "agenda",
                table: "agenda_entries",
                columns: new[] { "company_id", "activity_type" });

            migrationBuilder.CreateIndex(
                name: "ix_agenda_entries_company_start",
                schema: "agenda",
                table: "agenda_entries",
                columns: new[] { "company_id", "start_date_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenda_entries",
                schema: "agenda");
        }
    }
}
