using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SISLAB.Infrastructure.Data;

namespace SISLAB.Modules.Identity.Infrastructure.Authorization;

/// <summary>
/// Post-discovery hosted service that stamps the pt-BR <see cref="PermissionDisplayNames"/> onto Lumen's
/// permission catalogue (<c>"Lumen"."Permission"."DisplayName"</c>), so the profile-management UI shows
/// Portuguese labels instead of the raw <c>&lt;Controller&gt;.&lt;Action&gt;</c> codes.
///
/// <para><b>Ordering is the whole design.</b> Registered <i>after</i> <c>AddLumenAuthorizationDiscovery()</c>
/// in <c>IdentityModuleServiceExtensions</c>. Hosted services run sequentially in registration order, so by
/// the time this executes the discovery has already materialized and normalized every permission row. This
/// seeder is therefore the <b>last write</b> and wins over discovery, leaving the pt-BR label in the source of
/// truth (the database). A migration would lose that race — discovery would overwrite the seeded value on the
/// next boot — which is why this is a boot-time, auto-corrective seeder rather than a migration.</para>
///
/// <para><b>Idempotent and non-destructive.</b> Only an <c>UPDATE ... WHERE "Code" = @Code</c> per catalogue
/// entry — never an <c>INSERT</c> (discovery already guarantees the row exists for every gated endpoint).
/// Codes that are catalogued but not <c>[RequirePermission]</c>-gated (e.g. the read-only Audit endpoints)
/// simply update zero rows, which is a harmless no-op. Running on every boot keeps the labels self-healing.</para>
///
/// <para><b>Resilient.</b> Like the dev seed, a failure here is logged but never crashes the application:
/// missing display labels degrade the UI, they are not an availability prerequisite.</para>
/// </summary>
internal sealed class PermissionDisplayNameSeeder : IHostedService
{
    /// <summary>
    /// Idempotent per-code update. PostgreSQL: schema and PascalCase columns are quoted exactly as Lumen's
    /// migrations create them (<c>"Lumen"."Permission"</c>, <c>"DisplayName"</c>, <c>"Code"</c>). No <c>INSERT</c>:
    /// discovery owns row creation.
    /// </summary>
    private const string UpdateSql =
        """
        UPDATE "Lumen"."Permission"
        SET "DisplayName" = @DisplayName
        WHERE "Code" = @Code;
        """;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PermissionDisplayNameSeeder> _logger;

    public PermissionDisplayNameSeeder(
        IServiceScopeFactory scopeFactory,
        ILogger<PermissionDisplayNameSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Seeding pt-BR permission display names ({Count} codes)...",
                PermissionDisplayNames.ByCode.Count);

            using IServiceScope scope = _scopeFactory.CreateScope();
            DbConnectionFactory connectionFactory =
                scope.ServiceProvider.GetRequiredService<DbConnectionFactory>();

            using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync();

            int updatedRows = 0;
            foreach (KeyValuePair<string, string> entry in PermissionDisplayNames.ByCode)
            {
                updatedRows += await connection.ExecuteAsync(new CommandDefinition(
                    UpdateSql,
                    new { Code = entry.Key, DisplayName = entry.Value },
                    cancellationToken: cancellationToken));
            }

            _logger.LogInformation(
                "pt-BR permission display names seeded: {Updated}/{Total} rows updated " +
                "(codes without a materialized permission row are a no-op).",
                updatedRows, PermissionDisplayNames.ByCode.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding pt-BR permission display names failed. Boot continues.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
