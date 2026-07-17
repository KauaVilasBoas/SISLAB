using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Audit.Application.AuditRead;

/// <summary>
/// Read-side query (card [E9] #57) that returns the <b>full</b> audit trail of the active company matching
/// the same filters as the listing — no pagination — so the controller can stream it as a CSV attachment.
/// Reuses <see cref="AuditEntryListItem"/> as the export row shape and the shared parameter set for the
/// tenant guard, entity/action filters and the inclusive date window.
/// </summary>
public sealed record ExportAuditEntriesQuery : IQuery<IReadOnlyList<AuditEntryListItem>>
{
    /// <summary>Optional filter: only entries affecting this entity type.</summary>
    public string? EntityType { get; init; }

    /// <summary>Optional filter: only entries affecting this specific entity id.</summary>
    public Guid? EntityId { get; init; }

    /// <summary>Optional filter: only entries of this business action.</summary>
    public string? Action { get; init; }

    /// <summary>Optional inclusive lower bound on the occurred-at date (UTC).</summary>
    public DateOnly? From { get; init; }

    /// <summary>Optional inclusive upper bound on the occurred-at date (UTC).</summary>
    public DateOnly? To { get; init; }
}

internal sealed class ExportAuditEntriesQueryHandler
    : BaseDataAccess, IQueryHandler<ExportAuditEntriesQuery, IReadOnlyList<AuditEntryListItem>>
{
    // Same predicate as the listing, without the ROW_NUMBER window — the whole matching set, newest first.
    private const string Sql =
        """
        SELECT
            a.id,
            COALESCE(u."Email", u."Username", a.user_id) AS userid,
            a.action,
            a.entity_type     AS entitytype,
            a.entity_id       AS entityid,
            a.payload,
            a.occurred_at_utc AS occurredatutc
        FROM audit.audit_entries AS a
        LEFT JOIN identity."Users" AS u ON u."Id"::text = a.user_id
        WHERE a.company_id = @CompanyId
          AND (@EntityType::text        IS NULL OR a.entity_type = @EntityType)
          AND (@EntityId::uuid          IS NULL OR a.entity_id = @EntityId)
          AND (@Action::text            IS NULL OR a.action = @Action)
          AND (@From::timestamp         IS NULL OR a.occurred_at_utc >= @From)
          AND (@ToExclusive::timestamp  IS NULL OR a.occurred_at_utc < @ToExclusive)
        ORDER BY a.occurred_at_utc DESC, a.id DESC;
        """;

    private readonly ITenantContext _tenantContext;

    public ExportAuditEntriesQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<IReadOnlyList<AuditEntryListItem>> HandleAsync(
        ExportAuditEntriesQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        AuditEntriesQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<AuditEntryListItem> items = (await connection.QueryAsync<AuditEntryListItem>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        return items;
    }

    /// <summary>
    /// Materializes the Dapper parameter set for the export. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request). Extracted so the tenant guard, filters and window
    /// are unit-testable without a live database.
    /// </summary>
    internal AuditEntriesQueryParameters BuildParameters(ExportAuditEntriesQuery request) =>
        AuditEntriesQueryParameters.ForExport(_tenantContext.CompanyId, request);
}
