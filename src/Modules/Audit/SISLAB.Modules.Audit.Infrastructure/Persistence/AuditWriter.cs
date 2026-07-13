using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Audit.Contracts;

namespace SISLAB.Modules.Audit.Infrastructure.Persistence;

/// <summary>
/// Append-only <see cref="IAuditWriter"/> backed by a direct Dapper INSERT into
/// <c>audit.audit_entries</c> (card [E9] #57).
///
/// No EF Core: the row is write-once, so there is no aggregate to track or update. The INSERT is
/// idempotent via <c>ON CONFLICT (id) DO NOTHING</c>, so a retried operation never duplicates the trail.
/// The write opens its own connection (independent of any caller transaction) so the audit record is
/// durable even when a later step in the request fails.
/// </summary>
internal sealed class AuditWriter : BaseDataAccess, IAuditWriter
{
    private const string InsertSql =
        """
        INSERT INTO audit.audit_entries
            (id, company_id, user_id, action, entity_type, entity_id, payload, occurred_at_utc)
        VALUES
            (@Id, @CompanyId, @UserId, @Action, @EntityType, @EntityId, @Payload::jsonb, @OccurredAtUtc)
        ON CONFLICT (id) DO NOTHING;
        """;

    public AuditWriter(DbConnectionFactory connectionFactory) : base(connectionFactory) { }

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        var parameters = new
        {
            entry.Id,
            entry.CompanyId,
            entry.UserId,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.Payload,
            entry.OccurredAtUtc
        };

        await connection.ExecuteAsync(
            new CommandDefinition(InsertSql, parameters, cancellationToken: cancellationToken));
    }
}
