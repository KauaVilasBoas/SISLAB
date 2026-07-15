using Microsoft.EntityFrameworkCore;

namespace SISLAB.Migrations;

/// <summary>
/// Entity-less <see cref="DbContext"/> used exclusively to run idempotent SQL seed migrations that
/// populate Lumen's permission catalogue (<c>"Lumen"."PermissionGroup"</c> / <c>"Lumen"."Permission"</c>).
///
/// <para>It owns no <see cref="DbSet{TEntity}"/>: the permission tables belong to Lumen's own DbContext and
/// migrations (applied on app boot). This context only carries a migration history in its own <c>seed</c>
/// schema and emits the reference-data <c>INSERT</c>s through the <c>SeedLumenPermission*</c> helpers, keeping
/// the seed decoupled from both the app boot path and SISLAB's module DbContexts.</para>
/// </summary>
public sealed class SislabSeedDbContext(DbContextOptions<SislabSeedDbContext> options) : DbContext(options)
{
    // No entities — used exclusively to run idempotent SQL seed migrations against the Lumen schema.
}
