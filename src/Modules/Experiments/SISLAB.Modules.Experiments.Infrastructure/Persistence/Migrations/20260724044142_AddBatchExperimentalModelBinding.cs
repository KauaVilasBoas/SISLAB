using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the nullable <c>experimental_model_id</c> column to <c>experiments.project_batches</c> (SISLAB-04):
    /// the id, held by value, of the Configuration experimental model the batch runs. No cross-module foreign key
    /// (the referenced aggregate lives in the Configuration bounded context) — it is a plain uuid. Nullable so
    /// existing and freshly-planned batches migrate without a model bound.
    /// </summary>
    /// <inheritdoc />
    public partial class AddBatchExperimentalModelBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "experimental_model_id",
                schema: "experiments",
                table: "project_batches",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "experimental_model_id",
                schema: "experiments",
                table: "project_batches");
        }
    }
}
