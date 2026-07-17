namespace SISLAB.Modules.Audit.Application.AuditRead;

/// <summary>
/// Immutable Dapper parameter set shared by the audit-trail listing and CSV export (card [E9] #57). The
/// property names match the <c>@Parameter</c> tokens in both SQL statements exactly (Dapper binds by name).
/// Exposed to the module's tests so the tenant guard, filters and the inclusive date window can be asserted
/// without a live database.
/// </summary>
/// <remarks>
/// <see cref="ToExclusive"/> is the caller's inclusive <c>To</c> date plus one day, converted to a
/// <see cref="DateTime"/> — so the SQL uses a half-open <c>occurred_at_utc &lt; @ToExclusive</c> range that
/// includes every timestamp on the <c>To</c> day, avoiding the off-by-one that a plain <c>&lt;= @To</c> on a
/// midnight boundary would cause.
/// </remarks>
internal sealed record AuditEntriesQueryParameters(
    Guid CompanyId,
    string? EntityType,
    Guid? EntityId,
    string? Action,
    DateTime? From,
    DateTime? ToExclusive,
    int FirstResult,
    int LastResult)
{
    /// <summary>Builds the parameter set for a paginated listing.</summary>
    public static AuditEntriesQueryParameters ForPage(Guid companyId, ListAuditEntriesQuery request) => new(
        CompanyId: companyId,
        EntityType: Normalize(request.EntityType),
        EntityId: request.EntityId,
        Action: Normalize(request.Action),
        From: ToStartOfDay(request.From),
        ToExclusive: ToExclusiveUpperBound(request.To),
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>Builds the parameter set for the unpaginated CSV export (no row-number bounds).</summary>
    public static AuditEntriesQueryParameters ForExport(Guid companyId, ExportAuditEntriesQuery request) => new(
        CompanyId: companyId,
        EntityType: Normalize(request.EntityType),
        EntityId: request.EntityId,
        Action: Normalize(request.Action),
        From: ToStartOfDay(request.From),
        ToExclusive: ToExclusiveUpperBound(request.To),
        FirstResult: 0,
        LastResult: 0);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? ToStartOfDay(DateOnly? date) =>
        date.HasValue ? date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;

    private static DateTime? ToExclusiveUpperBound(DateOnly? date) =>
        date.HasValue ? date.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
}
