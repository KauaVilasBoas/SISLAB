using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_plans",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_plans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "collection_role_assignments",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_role_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_role_assignments_collection_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "experiments",
                        principalTable: "collection_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_sample_routings",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sample_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    storage_room_id = table.Column<Guid>(type: "uuid", nullable: true),
                    storage_label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    conservation_temp_min = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    conservation_temp_max = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_sample_routings", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_sample_routings_collection_plans_plan_id",
                        column: x => x.plan_id,
                        principalSchema: "experiments",
                        principalTable: "collection_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "collection_planned_analyses",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    routing_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_planned_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_planned_analyses_collection_sample_routings_rout~",
                        column: x => x.routing_id,
                        principalSchema: "experiments",
                        principalTable: "collection_sample_routings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collection_planned_analyses_routing_id",
                schema: "experiments",
                table: "collection_planned_analyses",
                column: "routing_id");

            migrationBuilder.CreateIndex(
                name: "ix_collection_plans_company_id_id",
                schema: "experiments",
                table: "collection_plans",
                columns: new[] { "company_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_collection_plans_company_id_batch_id",
                schema: "experiments",
                table: "collection_plans",
                columns: new[] { "company_id", "batch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_collection_role_assignments_plan_id_role_id",
                schema: "experiments",
                table: "collection_role_assignments",
                columns: new[] { "plan_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_collection_sample_routings_plan_id_sample_type",
                schema: "experiments",
                table: "collection_sample_routings",
                columns: new[] { "plan_id", "sample_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_planned_analyses",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "collection_role_assignments",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "collection_sample_routings",
                schema: "experiments");

            migrationBuilder.DropTable(
                name: "collection_plans",
                schema: "experiments");
        }
    }
}
