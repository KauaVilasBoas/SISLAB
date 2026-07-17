using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialExperimentsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "experiments");

            migrationBuilder.CreateTable(
                name: "experiments",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    compound_partner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    formula_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    formula_expression = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    formula_applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    formula_result_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_experiments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dead_lettered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "experiment_steps",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_order = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    performed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    performed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    experiment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_experiment_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_experiment_steps_experiments_experiment_id",
                        column: x => x.experiment_id,
                        principalSchema: "experiments",
                        principalTable: "experiments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wells",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    well_row = table.Column<char>(type: "character(1)", nullable: false),
                    well_column = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concentration_um = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    sample_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    raw_absorbance = table.Column<decimal>(type: "numeric(12,4)", nullable: true),
                    experiment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wells", x => x.id);
                    table.ForeignKey(
                        name: "FK_wells_experiments_experiment_id",
                        column: x => x.experiment_id,
                        principalSchema: "experiments",
                        principalTable: "experiments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_experiment_steps_experiment_id",
                schema: "experiments",
                table: "experiment_steps",
                column: "experiment_id");

            migrationBuilder.CreateIndex(
                name: "ix_experiments_company_id_id",
                schema: "experiments",
                table: "experiments",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                schema: "experiments",
                table: "outbox_messages",
                column: "occurred_on_utc",
                filter: "processed_at_utc IS NULL AND dead_lettered_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_wells_experiment_id",
                schema: "experiments",
                table: "wells",
                column: "experiment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "experiment_steps",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "wells",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "experiments",
                schema: "experiments");
        }
    }
}
