using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.Partners.Queries;

/// <summary>
/// Read-side query (card [E4] #28) that lists the partners (suppliers/clients) of the <b>active company</b> for
/// the "Parceiros" screen (#48), with optional filters by type and free-text search, ordered by name and
/// paginated. It reads the <c>inventory.partners</c> table directly via Dapper — never the write DbContext — and
/// projects the flat <see cref="PartnerListItem"/> the UI table needs.
/// </summary>
/// <remarks>
/// <para>
/// By default the listing hides inactive (deactivated) partners; <see cref="IncludeInactive"/> brings them back.
/// The registration document is exposed as <c>cnpj</c> (its most common form for Brazilian suppliers), mapping
/// onto the aggregate's free-text <c>document</c>.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global query
/// filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record ListPartnersQuery : PagedQuery<PagedResult<PartnerListItem>>
{
    /// <summary>Optional partner-type filter (Supplier/Client/Both); null lists every type.</summary>
    public PartnerType? Type { get; init; }

    /// <summary>Optional free-text search matched (ILIKE) against name and document (cnpj).</summary>
    public string? Search { get; init; }

    /// <summary>When false (default), inactive (deactivated) partners are hidden from the listing.</summary>
    public bool IncludeInactive { get; init; }
}

/// <summary>
/// Flat read row for the partners table (card [E4] #28). Enxuto by design: it exposes the primitives the UI
/// renders directly — identity, role, document and active flag — and never leaks the <c>Partner</c> aggregate or
/// its value objects.
/// </summary>
public sealed record PartnerListItem(
    Guid Id,
    string Name,
    PartnerType Type,
    string? Cnpj,
    bool IsActive);

internal sealed class ListPartnersQueryHandler
    : BaseDataAccess, IQueryHandler<ListPartnersQuery, PagedResult<PartnerListItem>>
{
    // The type is stored as the enum name, so the optional filter compares against the @Type string param.
    // is_active is a stored column, so the default listing prunes inactive partners in the WHERE unless
    // @IncludeInactive. document surfaces as cnpj. company_id keeps the mandatory tenant scoping (read side has
    // no EF query filter).
    private const string Sql =
        """
        WITH records AS (
            SELECT
                p.id,
                p.name,
                p.type,
                p.document,
                p.is_active,
                ROW_NUMBER() OVER (ORDER BY p.name ASC, p.id ASC) AS row_number,
                (COUNT(*)    OVER ())::int                        AS total_rows
            FROM inventory.partners AS p
            WHERE p.company_id = @CompanyId
              AND (@IncludeInactive OR p.is_active)
              AND (@Type IS NULL OR p.type = @Type)
              AND (
                    @Search IS NULL
                    OR p.name ILIKE '%' || @Search || '%'
                    OR p.document ILIKE '%' || @Search || '%'
                  )
        )
        SELECT
            id,
            name,
            type,
            document  AS cnpj,
            is_active AS isactive,
            total_rows AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;

    public ListPartnersQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PagedResult<PartnerListItem>> HandleAsync(
        ListPartnersQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        PartnerListQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<PartnerListRow> rows = (await connection.QueryAsync<PartnerListRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<PartnerListItem> items = rows
            .Select(row => row.ToListItem())
            .ToList();

        return new PagedResult<PartnerListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), a blank search collapses to null and the type filter is
    /// carried as its enum name — extracted so the tenant guard and filter normalization are unit-testable without
    /// a live database.
    /// </summary>
    internal PartnerListQueryParameters BuildParameters(ListPartnersQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        Type: request.Type?.ToString(),
        Search: NormalizeFilter(request.Search),
        IncludeInactive: request.IncludeInactive,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>Trims a filter and collapses a blank value to null, so an empty box means "no filter".</summary>
    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>) alongside
    /// the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record PartnerListRow(
        Guid Id,
        string Name,
        PartnerType Type,
        string? Cnpj,
        bool IsActive,
        int TotalRows)
    {
        public PartnerListItem ToListItem() => new(Id, Name, Type, Cnpj, IsActive);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListPartnersQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the tenant
/// guard and filter normalization can be asserted without a live database.
/// </summary>
internal sealed record PartnerListQueryParameters(
    Guid CompanyId,
    string? Type,
    string? Search,
    bool IncludeInactive,
    int FirstResult,
    int LastResult);
