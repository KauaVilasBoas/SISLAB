using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Authorization;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Boot-time seeder that makes SISLAB the owner of the Lumen permission catalogue.
///
/// <para><b>Why a seeder and not an EF migration.</b> With Lumen.Authorization 2.0.0 the catalogue ownership
/// inverted: the library no longer writes permissions (default <c>CatalogMode = Validate</c> only scans every
/// <c>[RequirePermission]</c> code and logs a warning for the ones missing from the database). The catalogue
/// tables (<c>"Lumen"."PermissionGroup"</c>, <c>"Lumen"."Permission"</c>) live in Lumen's own DbContext and
/// schema, whose migrations are applied on boot by <c>LumenAuthorizationStartupService</c>. SISLAB's
/// <c>IdentityDbContext</c> owns a different schema/history and its migration hosted service runs BEFORE Lumen
/// creates its schema, so an EF migration seeding <c>"Lumen"."Permission"</c> from IdentityDbContext would hit
/// <c>42P01 (relation does not exist)</c>. Instead this seeder runs as a hosted service registered immediately
/// after the Lumen umbrella (hosted services execute in registration order), when the tables already exist —
/// the same seam <c>LafteDevSeeder</c> uses to write Lumen's own tables.</para>
///
/// <para><b>Idempotent and non-destructive.</b> Every row uses <c>INSERT ... ON CONFLICT ("Id") DO NOTHING</c>
/// with deterministic UUIDs, so re-runs are no-ops and never duplicate. The seeder never updates or deletes:
/// once a permission exists, its labelling and grouping are stable. Codes SISLAB stops gating simply stay in
/// the catalogue (harmless) until an explicit migration removes them.</para>
///
/// <para><b>Resilient.</b> A failure here is logged but never crashes the application. A missing catalogue row
/// only degrades authorization validation to a boot-time warning (Validate mode), it is not an availability
/// prerequisite.</para>
/// </summary>
internal sealed class LumenPermissionCatalogSeeder : IHostedService
{
    /// <summary>
    /// Idempotent group upsert. PostgreSQL: schema and PascalCase columns are quoted exactly as Lumen's EF
    /// configuration maps them (<c>"Lumen"."PermissionGroup"</c>; required columns <c>"Name"</c>,
    /// <c>"Description"</c>; soft-delete flags default to not-deleted).
    /// </summary>
    private const string InsertGroupSql =
        """
        INSERT INTO "Lumen"."PermissionGroup" ("Id", "Name", "Description", "IsDeleted", "DeletedAt")
        VALUES (@Id, @Name, @Description, false, NULL)
        ON CONFLICT ("Id") DO NOTHING;
        """;

    /// <summary>
    /// Idempotent permission upsert. Populates every NOT NULL column Lumen requires: <c>"Code"</c>,
    /// <c>"Controller"</c> and <c>"Action"</c> (the two halves of the code), the pt-BR <c>"DisplayName"</c>,
    /// the <c>"GroupPermissionId"</c> and the orphan/soft-delete flags (defaulted to false/NULL).
    /// </summary>
    private const string InsertPermissionSql =
        """
        INSERT INTO "Lumen"."Permission"
            ("Id", "Code", "Controller", "Action", "DisplayName", "GroupPermissionId",
             "IsOrphan", "OrphanedAt", "IsDeleted", "DeletedAt")
        VALUES
            (@Id, @Code, @Controller, @Action, @DisplayName, @GroupPermissionId,
             false, NULL, false, NULL)
        ON CONFLICT ("Id") DO NOTHING;
        """;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LumenPermissionCatalogSeeder> _logger;

    public LumenPermissionCatalogSeeder(
        IServiceScopeFactory scopeFactory,
        ILogger<LumenPermissionCatalogSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<PermissionCatalog.Group> groups = PermissionCatalog.Groups;
            int permissionCount = groups.Sum(g => g.Permissions.Count);

            _logger.LogInformation(
                "Seeding SISLAB permission catalogue ({Groups} groups, {Permissions} permissions)...",
                groups.Count, permissionCount);

            using IServiceScope scope = _scopeFactory.CreateScope();
            DbConnectionFactory connectionFactory =
                scope.ServiceProvider.GetRequiredService<DbConnectionFactory>();

            using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync();

            foreach (PermissionCatalog.Group group in groups)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    InsertGroupSql,
                    new { group.Id, group.Name, group.Description },
                    cancellationToken: cancellationToken));

                foreach (PermissionCatalog.Permission permission in group.Permissions)
                {
                    (string controller, string action) = SplitCode(permission.Code);

                    await connection.ExecuteAsync(new CommandDefinition(
                        InsertPermissionSql,
                        new
                        {
                            permission.Id,
                            permission.Code,
                            Controller = controller,
                            Action = action,
                            permission.DisplayName,
                            GroupPermissionId = group.Id
                        },
                        cancellationToken: cancellationToken));
                }
            }

            _logger.LogInformation("SISLAB permission catalogue seeded (idempotent).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding SISLAB permission catalogue failed. Boot continues.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Splits a <c>&lt;Controller&gt;.&lt;Action&gt;</c> code into its two halves, matching how Lumen derives
    /// the <c>Controller</c>/<c>Action</c> columns from the code. A code without a dot maps to itself on both.
    /// </summary>
    private static (string Controller, string Action) SplitCode(string code)
    {
        string[] parts = code.Split('.', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (code, code);
    }
}
