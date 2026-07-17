using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInVivoProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    species = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_design_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_batches",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    design_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_batches_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "experiments",
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_groups",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    dose_amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    dose_unit = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_groups", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_groups_project_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "experiments",
                        principalTable: "project_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_animals",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    weight_grams = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_animals", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_animals_project_groups_group_id",
                        column: x => x.group_id,
                        principalSchema: "experiments",
                        principalTable: "project_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_animals_group_id",
                schema: "experiments",
                table: "project_animals",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_batches_project_id",
                schema: "experiments",
                table: "project_batches",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_groups_batch_id",
                schema: "experiments",
                table: "project_groups",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_projects_company_id_id",
                schema: "experiments",
                table: "projects",
                columns: new[] { "company_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_animals",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "project_groups",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "project_batches",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "experiments");
        }
    }
}
