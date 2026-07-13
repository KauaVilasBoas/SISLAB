using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Inventory.Domain.Partners;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Application.PartnerRead;

/// <summary>
/// Read-side query (card [E4] #28) that loads the single partner of the <b>active company</b> identified by
/// <see cref="PartnerId"/>, or <see langword="null"/> when no such partner exists for that company. It reads the
/// <c>inventory.partners</c> table via Dapper — never the write DbContext — and projects the flat
/// <see cref="PartnerDetail"/> the partner detail panel needs.
/// </summary>
/// <remarks>
/// <para>
/// The detail extends the listing row with the contact e-mail and the free-text description ("what the partner
/// supplies/does"): <c>email</c> maps onto the aggregate's <c>contact_email</c> and <c>notes</c> onto its
/// <c>description</c>.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global query
/// filter (defense-in-depth, section 7). An id belonging to another company is indistinguishable from a
/// non-existent one: both yield <see langword="null"/>, which the controller maps to a 404.
/// </para>
/// </remarks>
public sealed record GetPartnerDetailQuery(Guid PartnerId) : IQuery<PartnerDetail?>;

/// <summary>
/// Flat read row for a single partner (card [E4] #28). Extends the listing projection with the contact e-mail and
/// the free-text description/notes. Enxuto by design: it never leaks the <c>Partner</c> aggregate or its value
/// objects (the <c>Email</c> value object surfaces as a plain string).
/// </summary>
public sealed record PartnerDetail(
    Guid Id,
    string Name,
    PartnerType Type,
    string? Cnpj,
    bool IsActive,
    string? Email,
    string? Notes);

internal sealed class GetPartnerDetailQueryHandler
    : BaseDataAccess, IQueryHandler<GetPartnerDetailQuery, PartnerDetail?>
{
    // Single-row lookup by (company_id, id): no pagination window is needed. document surfaces as cnpj,
    // contact_email as email and description as notes (the aggregate's free-text "what it supplies/does").
    // company_id keeps the mandatory tenant scoping, so an id from another tenant returns no row (→ null),
    // exactly like a missing id. Columns are aliased to the PartnerDetail property names (Dapper binds by name).
    private const string Sql =
        """
        SELECT
            p.id            AS id,
            p.name          AS name,
            p.type          AS type,
            p.document      AS cnpj,
            p.is_active     AS isactive,
            p.contact_email AS email,
            p.description   AS notes
        FROM inventory.partners AS p
        WHERE p.company_id = @CompanyId
          AND p.id = @PartnerId;
        """;

    private readonly ITenantContext _tenantContext;

    public GetPartnerDetailQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<PartnerDetail?> HandleAsync(
        GetPartnerDetailQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        PartnerDetailQueryParameters parameters = BuildParameters(request);

        return await connection.QuerySingleOrDefaultAsync<PartnerDetail>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request) — extracted so the tenant guard is unit-testable without a
    /// live database.
    /// </summary>
    internal PartnerDetailQueryParameters BuildParameters(GetPartnerDetailQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        PartnerId: request.PartnerId);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetPartnerDetailQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the tenant
/// guard can be asserted without a live database.
/// </summary>
internal sealed record PartnerDetailQueryParameters(
    Guid CompanyId,
    Guid PartnerId);
