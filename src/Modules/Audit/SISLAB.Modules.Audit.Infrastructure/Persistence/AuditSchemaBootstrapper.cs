using System.Data;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SISLAB.Infrastructure.Data;

namespace SISLAB.Modules.Audit.Infrastructure.Persistence;

/// <summary>
/// Creates the <c>audit</c> schema and the append-only <c>audit.audit_entries</c> table on startup
/// (card [E9] #57), idempotently.
///
/// The Audit module has no EF DbContext (the trail is Dapper-only, write-once), so instead of EF
/// migrations it applies a small <c>CREATE ... IF NOT EXISTS</c> DDL script — the same "each module owns
/// its schema at boot" convention the EF-backed modules follow, minus the EF ceremony. The DDL is safe to
/// run on every boot.
/// </summary>
internal sealed class AuditSchemaBootstrapper : IHostedService
{
    private const string Ddl =
        """
        CREATE SCHEMA IF NOT EXISTS audit;

        CREATE TABLE IF NOT EXISTS audit.audit_entries (
            id              uuid        NOT NULL PRIMARY KEY,
            company_id      uuid        NOT NULL,
            user_id         text        NOT NULL,
            action          text        NOT NULL,
            entity_type     text        NOT NULL,
            entity_id       uuid        NOT NULL,
            payload         jsonb       NOT NULL,
            occurred_at_utc timestamptz NOT NULL
        );

        -- Tenant-scoped, newest-first listing/export is the only read pattern (card #57).
        CREATE INDEX IF NOT EXISTS ix_audit_entries_company_occurred
            ON audit.audit_entries (company_id, occurred_at_utc DESC);

        -- Narrowing by entity type / action within a company.
        CREATE INDEX IF NOT EXISTS ix_audit_entries_company_entity_type
            ON audit.audit_entries (company_id, entity_type);
        """;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditSchemaBootstrapper> _logger;

    public AuditSchemaBootstrapper(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditSchemaBootstrapper> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying SISLAB Audit schema (schema 'audit')...");

        using IServiceScope scope = _scopeFactory.CreateScope();
        DbConnectionFactory connectionFactory =
            scope.ServiceProvider.GetRequiredService<DbConnectionFactory>();

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(
            new CommandDefinition(Ddl, cancellationToken: cancellationToken));

        _logger.LogInformation("SISLAB Audit schema applied.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
