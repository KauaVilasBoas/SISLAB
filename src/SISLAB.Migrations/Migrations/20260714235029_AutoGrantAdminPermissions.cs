using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SISLAB.Migrations.Migrations
{
    /// <summary>
    /// Auto-wires the <c>Administrator</c> profile to every permission via two PostgreSQL triggers, so the
    /// permission catalogue and the Administrator grant stay in sync without any manual grant rows in future
    /// migrations. This is a data-plumbing migration: it emits raw SQL only (no model changes).
    ///
    /// <list type="number">
    /// <item><description><c>trg_auto_grant_permission_to_administrator</c> — fires <c>AFTER INSERT</c> on
    /// <c>"Lumen"."Permission"</c>: links the new permission to the Administrator profile (resolved by name)
    /// if that profile exists and the grant is not already present.</description></item>
    /// <item><description><c>trg_auto_grant_all_permissions_to_new_administrator</c> — fires <c>AFTER INSERT</c>
    /// on <c>"Lumen"."Profile"</c>: when the Administrator profile itself is created, retroactively grants every
    /// currently active permission.</description></item>
    /// </list>
    ///
    /// <para>A closing <c>DO</c> block covers the ordering case where both the permissions and the Administrator
    /// profile already exist when this migration runs (e.g. the dev seeder created the profile before this
    /// migration was applied): it grants all active permissions not yet assigned. Every insert is guarded with
    /// <c>NOT EXISTS</c> — there is no unique constraint to <c>ON CONFLICT</c> against — so the operation is
    /// idempotent.</para>
    ///
    /// <para>The Administrator profile is thus the single "grant-all" profile; adding a new
    /// <c>SeedLumenPermission</c> in any later migration requires no companion grant step.</para>
    /// </summary>
    public partial class AutoGrantAdminPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Trigger function: when a new permission is inserted, auto-grant it to Administrator.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "Lumen"."auto_grant_permission_to_administrator"()
                RETURNS TRIGGER LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO "Lumen"."PermissionProfile" ("Id", "PermissionId", "ProfileId", "IsDeleted", "DeletedAt")
                    SELECT gen_random_uuid(), NEW."Id", p."Id", false, null
                    FROM "Lumen"."Profile" p
                    WHERE p."Name" = 'Administrator' AND p."IsDeleted" = false
                      AND NOT EXISTS (
                          SELECT 1 FROM "Lumen"."PermissionProfile" pp
                          WHERE pp."PermissionId" = NEW."Id" AND pp."ProfileId" = p."Id" AND pp."IsDeleted" = false
                      );
                    RETURN NEW;
                END;
                $$;
                """);

            // 2. Bind trigger to Permission table.
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_auto_grant_permission_to_administrator ON "Lumen"."Permission";
                CREATE TRIGGER trg_auto_grant_permission_to_administrator
                    AFTER INSERT ON "Lumen"."Permission"
                    FOR EACH ROW EXECUTE FUNCTION "Lumen"."auto_grant_permission_to_administrator"();
                """);

            // 3. Trigger function: when the Administrator profile is created, grant all existing permissions.
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION "Lumen"."auto_grant_all_permissions_to_new_administrator"()
                RETURNS TRIGGER LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Name" = 'Administrator' AND NEW."IsDeleted" = false THEN
                        INSERT INTO "Lumen"."PermissionProfile" ("Id", "PermissionId", "ProfileId", "IsDeleted", "DeletedAt")
                        SELECT gen_random_uuid(), perm."Id", NEW."Id", false, null
                        FROM "Lumen"."Permission" perm
                        WHERE perm."IsDeleted" = false
                          AND NOT EXISTS (
                              SELECT 1 FROM "Lumen"."PermissionProfile" pp
                              WHERE pp."PermissionId" = perm."Id" AND pp."ProfileId" = NEW."Id" AND pp."IsDeleted" = false
                          );
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);

            // 4. Bind trigger to Profile table.
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_auto_grant_all_permissions_to_new_administrator ON "Lumen"."Profile";
                CREATE TRIGGER trg_auto_grant_all_permissions_to_new_administrator
                    AFTER INSERT ON "Lumen"."Profile"
                    FOR EACH ROW EXECUTE FUNCTION "Lumen"."auto_grant_all_permissions_to_new_administrator"();
                """);

            // 5. Retroactive grant: if Administrator already exists (dev seeder ran before this migration),
            //    grant all current permissions that aren't yet assigned.
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    v_admin_id uuid;
                BEGIN
                    SELECT "Id" INTO v_admin_id
                    FROM "Lumen"."Profile"
                    WHERE "Name" = 'Administrator' AND "IsDeleted" = false
                    LIMIT 1;

                    IF v_admin_id IS NOT NULL THEN
                        INSERT INTO "Lumen"."PermissionProfile" ("Id", "PermissionId", "ProfileId", "IsDeleted", "DeletedAt")
                        SELECT gen_random_uuid(), p."Id", v_admin_id, false, null
                        FROM "Lumen"."Permission" p
                        WHERE p."IsDeleted" = false
                          AND NOT EXISTS (
                              SELECT 1 FROM "Lumen"."PermissionProfile" pp
                              WHERE pp."PermissionId" = p."Id" AND pp."ProfileId" = v_admin_id AND pp."IsDeleted" = false
                          );
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS trg_auto_grant_permission_to_administrator ON "Lumen"."Permission";
                DROP FUNCTION IF EXISTS "Lumen"."auto_grant_permission_to_administrator"();
                DROP TRIGGER IF EXISTS trg_auto_grant_all_permissions_to_new_administrator ON "Lumen"."Profile";
                DROP FUNCTION IF EXISTS "Lumen"."auto_grant_all_permissions_to_new_administrator"();
                """);

            // PermissionProfile data is left in place — no rollback for reference data.
        }
    }
}
