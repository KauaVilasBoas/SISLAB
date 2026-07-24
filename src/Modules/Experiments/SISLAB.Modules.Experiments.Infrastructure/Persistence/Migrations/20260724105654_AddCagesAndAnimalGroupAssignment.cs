using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// SISLAB-03 — introduces the cage (caixa) as the animal's physical housing unit and turns the treatment group
    /// into an optional, by-value assignment on the animal (<c>group_id</c> becomes nullable, no FK).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Data preservation (Up).</b> Legacy <c>project_animals.group_id</c> is <c>NOT NULL</c> and each animal was
    /// owned by a group. The animal↔group link is preserved unchanged: <c>group_id</c> is merely relaxed to nullable
    /// and its FK dropped (the group is now resolved within the aggregate, not by a database FK). To give every legacy
    /// animal a cage without inventing an arbitrary grouping, one cage is synthesized <b>per legacy group</b> (in the
    /// same batch, named after the group), and each animal is placed into the cage synthesized from <i>its own</i>
    /// group. No animal is deleted, no group assignment is lost.
    /// </para>
    /// <para>
    /// <b>Reversal (Down).</b> The steps reverse exactly: the synthesized cages are dropped, <c>cage_id</c> is removed,
    /// and <c>group_id</c> is restored to <c>NOT NULL</c> with its FK. Because <c>group_id</c> was kept populated for
    /// every legacy animal, the round-trip is lossless for all data that predated this migration. Down requires that no
    /// animal created under the new model is left <i>unassigned</i> (<c>group_id IS NULL</c>) — such a row has no home
    /// in the old schema; the pre-condition is asserted so the reversal fails loudly rather than silently dropping it.
    /// </para>
    /// </remarks>
    public partial class AddCagesAndAnimalGroupAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Relax the animal→group relationship: drop the FK and make group_id nullable. The existing values are
            //    left untouched, so every legacy animal keeps its group assignment (now by value).
            migrationBuilder.DropForeignKey(
                name: "FK_project_animals_project_groups_group_id",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.AlterColumn<Guid>(
                name: "group_id",
                schema: "experiments",
                table: "project_animals",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // 2. Create the cage table (the animal's new physical parent).
            migrationBuilder.CreateTable(
                name: "project_cages",
                schema: "experiments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_cages", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_cages_project_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "experiments",
                        principalTable: "project_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_cages_batch_id",
                schema: "experiments",
                table: "project_cages",
                column: "batch_id");

            // 3. Add cage_id NULLABLE first so legacy rows can be back-filled before the NOT NULL constraint applies.
            migrationBuilder.AddColumn<Guid>(
                name: "cage_id",
                schema: "experiments",
                table: "project_animals",
                type: "uuid",
                nullable: true);

            // 4a. Synthesize one cage per legacy group that has animals (uncapped capacity; the group's name preserved
            //     so the migrated housing is recognizable). The cage id is derived deterministically from the group id
            //     (md5 of the group uuid, cast back to uuid) so step 4b can compute the same id without a temp table.
            migrationBuilder.Sql(
                """
                INSERT INTO experiments.project_cages (id, name, capacity, batch_id)
                SELECT
                    md5(g.id::text)::uuid                      AS id,
                    left('Caixa (migrada) — ' || g.name, 120)  AS name,
                    NULL                                       AS capacity,
                    g.batch_id                                 AS batch_id
                FROM experiments.project_groups AS g
                WHERE EXISTS (
                    SELECT 1 FROM experiments.project_animals AS a WHERE a.group_id = g.id
                );
                """);

            // 4b. Place each legacy animal into the cage synthesized from its own group. group_id is left intact, so
            //     the animal↔group link is preserved.
            migrationBuilder.Sql(
                """
                UPDATE experiments.project_animals AS a
                SET cage_id = md5(a.group_id::text)::uuid
                WHERE a.group_id IS NOT NULL
                  AND a.cage_id IS NULL;
                """);

            // 5. Enforce cage_id NOT NULL and wire the FK + index now that every legacy row has a cage.
            migrationBuilder.AlterColumn<Guid>(
                name: "cage_id",
                schema: "experiments",
                table: "project_animals",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_animals_cage_id",
                schema: "experiments",
                table: "project_animals",
                column: "cage_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_animals_group_id",
                schema: "experiments",
                table: "project_animals",
                column: "group_id");

            migrationBuilder.AddForeignKey(
                name: "FK_project_animals_project_cages_cage_id",
                schema: "experiments",
                table: "project_animals",
                column: "cage_id",
                principalSchema: "experiments",
                principalTable: "project_cages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Guard: the old schema cannot represent an unassigned animal (group_id NOT NULL). Fail loudly rather than
            // silently losing a row created under the new model.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM experiments.project_animals WHERE group_id IS NULL) THEN
                        RAISE EXCEPTION 'Cannot revert AddCagesAndAnimalGroupAssignment: % animal(s) are unassigned '
                            'to a group and have no place in the previous schema.',
                            (SELECT count(*) FROM experiments.project_animals WHERE group_id IS NULL);
                    END IF;
                END $$;
                """);

            // Drop the cage wiring (FK, indexes, table). group_id is untouched, so every animal keeps its group.
            migrationBuilder.DropForeignKey(
                name: "FK_project_animals_project_cages_cage_id",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropIndex(
                name: "ix_project_animals_cage_id",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropIndex(
                name: "ix_project_animals_group_id",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropColumn(
                name: "cage_id",
                schema: "experiments",
                table: "project_animals");

            migrationBuilder.DropTable(
                name: "project_cages",
                schema: "experiments");

            // Restore group_id to NOT NULL and re-establish the FK (every animal still has its group value).
            migrationBuilder.AlterColumn<Guid>(
                name: "group_id",
                schema: "experiments",
                table: "project_animals",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_project_animals_project_groups_group_id",
                schema: "experiments",
                table: "project_animals",
                column: "group_id",
                principalSchema: "experiments",
                principalTable: "project_groups",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
