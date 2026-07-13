using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Notifications.Application.NotificationsRead;

/// <summary>
/// Read-side query (card #64a — the bell badge) that counts, for the <b>active company</b>, how many
/// notifications are still unread. It is the single number the bell badge shows, resolved in one
/// tenant-scoped round-trip without pulling the list.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the single SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF
/// global query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record CountUnreadNotificationsQuery : IQuery<UnreadNotificationsCount>;

/// <summary>The bell badge value for the active company: how many notifications are currently unread.</summary>
public sealed record UnreadNotificationsCount(int UnreadCount);

internal sealed class CountUnreadNotificationsQueryHandler
    : BaseDataAccess, IQueryHandler<CountUnreadNotificationsQuery, UnreadNotificationsCount>
{
    // COUNT of unread notifications for the active company. A single row always comes back. company_id keeps
    // the mandatory tenant scoping (the read side has no EF global query filter).
    private const string Sql =
        """
        SELECT COUNT(*)::int AS unreadcount
        FROM notifications.notifications AS n
        WHERE n.company_id = @CompanyId
          AND n.is_read = false;
        """;

    private readonly ITenantContext _tenantContext;

    public CountUnreadNotificationsQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<UnreadNotificationsCount> HandleAsync(
        CountUnreadNotificationsQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        CountUnreadNotificationsQueryParameters parameters = BuildParameters();

        return await connection.QuerySingleAsync<UnreadNotificationsCount>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Materializes the Dapper parameter set: the company id always comes from <see cref="ITenantContext"/>
    /// (never the request). Extracted so the tenant guard is unit-testable without a live database.
    /// </summary>
    internal CountUnreadNotificationsQueryParameters BuildParameters() => new(
        CompanyId: _tenantContext.CompanyId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="CountUnreadNotificationsQuery"/>. The property name matches
/// the <c>@Parameter</c> token in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard can be asserted without a live database.
/// </summary>
internal sealed record CountUnreadNotificationsQueryParameters(Guid CompanyId);
