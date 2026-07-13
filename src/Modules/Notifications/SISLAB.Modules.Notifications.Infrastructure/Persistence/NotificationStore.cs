using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;

namespace SISLAB.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Dapper/Npgsql implementation of <see cref="INotificationStore"/>. Writes into
/// <c>notifications.notifications</c> with an idempotent insert: the <c>ON CONFLICT</c> targets the partial
/// unique index over active (unread) rows (<c>(company_id, dedupe_key) WHERE is_read = false</c>), so a
/// duplicate raise of the same active alert resolves to <c>DO NOTHING</c> instead of an exception.
/// </summary>
/// <remarks>
/// The insert appends <c>RETURNING id</c> so we can tell an actual insert (a row came back) from a skipped
/// conflict (no row came back). The <c>ON CONFLICT</c> conflict target must repeat the index's partial
/// predicate (<c>WHERE is_read = false</c>) — PostgreSQL matches a partial unique index only when the
/// statement's conflict target carries the same predicate.
/// </remarks>
internal sealed class NotificationStore : INotificationStore
{
    private const string InsertNotificationSql =
        """
        INSERT INTO notifications.notifications (
            id, company_id, type, severity, title, description,
            reference_target_type, reference_target_id, dedupe_key,
            is_read, created_at_utc, read_at_utc)
        VALUES (
            @Id, @CompanyId, @Type, @Severity, @Title, @Description,
            @ReferenceTargetType, @ReferenceTargetId, @DedupeKey,
            @IsRead, @CreatedAtUtc, @ReadAtUtc)
        ON CONFLICT (company_id, dedupe_key) WHERE is_read = false
        DO NOTHING
        RETURNING id;
        """;

    private readonly DbConnectionFactory _connectionFactory;

    public NotificationStore(DbConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<bool> TryAppendAsync(NotificationRow row, CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await _connectionFactory.CreateOpenConnectionAsync();

        Guid? insertedId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            InsertNotificationSql,
            new
            {
                row.Id,
                row.CompanyId,
                row.Type,
                row.Severity,
                row.Title,
                row.Description,
                row.ReferenceTargetType,
                row.ReferenceTargetId,
                row.DedupeKey,
                row.IsRead,
                row.CreatedAtUtc,
                row.ReadAtUtc
            },
            cancellationToken: cancellationToken));

        // A row id comes back only when a row was actually inserted; a skipped conflict returns nothing.
        return insertedId.HasValue;
    }
}
