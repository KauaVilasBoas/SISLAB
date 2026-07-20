using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomIdToAgendaEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "room_id",
                schema: "agenda",
                table: "agenda_entries",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "room_id",
                schema: "agenda",
                table: "agenda_entries");
        }
    }
}
