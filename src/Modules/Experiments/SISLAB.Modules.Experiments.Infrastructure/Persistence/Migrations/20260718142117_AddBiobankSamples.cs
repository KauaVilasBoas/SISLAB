using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBiobankSamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "samples",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    animal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_experiment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    collected_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    collected_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    conservation_temp_min = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    conservation_temp_max = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    storage_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    collected_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    collected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_samples", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sample_analyses",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consumed_value = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    consumed_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    performed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    performed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    result = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sample_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sample_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_sample_analyses_samples_sample_id",
                        column: x => x.sample_id,
                        principalSchema: "experiments",
                        principalTable: "samples",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sample_analyses_sample_id",
                schema: "experiments",
                table: "sample_analyses",
                column: "sample_id");

            migrationBuilder.CreateIndex(
                name: "ix_samples_company_id_id",
                schema: "experiments",
                table: "samples",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_samples_source_experiment_id",
                schema: "experiments",
                table: "samples",
                column: "source_experiment_id");

            migrationBuilder.CreateIndex(
                name: "ux_samples_company_id_code",
                schema: "experiments",
                table: "samples",
                columns: new[] { "company_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sample_analyses",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "samples",
                schema: "experiments");
        }
    }
}
