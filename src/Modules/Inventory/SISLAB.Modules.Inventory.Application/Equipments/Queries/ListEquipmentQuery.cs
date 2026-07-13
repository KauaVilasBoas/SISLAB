using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.Equipments.Queries;

/// <summary>
/// Read-side query (card [E4] #27) that lists the laboratory equipment of the <b>active company</b> for the
/// "Equipamentos" screen (#48), with optional filters by calibration status, storage location and free-text
/// search, ordered by name and paginated. It reads the <c>inventory.equipment</c> table directly via Dapper
/// (joined with <c>inventory.storage_locations</c> for the location name) — never the write DbContext — and
/// projects the flat <see cref="EquipmentListItem"/> the UI table needs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Derived calibration status.</b> Calibration is optional: an equipment with no planned
/// <c>next_calibration</c> reports <see cref="CalibrationStatus.NotRequired"/> (the "n/a" of the prototype).
/// Otherwise the status is computed in SQL against the handler-supplied <c>@Today</c> and a
/// <see cref="CalibrationStatusRule.DefaultDueSoonWindowDays"/>-day window: <see cref="CalibrationStatus.Overdue"/>
/// past its next date, <see cref="CalibrationStatus.DueSoon"/> within the window, otherwise
/// <see cref="CalibrationStatus.UpToDate"/> — a faithful mirror of <see cref="CalibrationStatusRule"/>. It is
/// never persisted (the domain derives "overdue" on demand from the schedule and a clock).
/// </para>
/// <para>
/// <b>Active flag.</b> <c>is_active</c> is derived from the operational status: an equipment counts as active
/// unless it is <see cref="EquipmentStatus.Inactive"/> (retired/decommissioned). By default the listing hides
/// inactive equipment; <see cref="IncludeInactive"/> brings them back.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and every SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global query
/// filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record ListEquipmentQuery : PagedQuery<PagedResult<EquipmentListItem>>
{
    /// <summary>Optional calibration-status filter; null lists every status (including NotRequired).</summary>
    public CalibrationStatus? Status { get; init; }

    /// <summary>Optional storage-location filter; null lists equipment of every location (and of none).</summary>
    public Guid? StorageLocationId { get; init; }

    /// <summary>Optional free-text search matched (ILIKE) against name and asset tag.</summary>
    public string? Search { get; init; }

    /// <summary>When false (default), inactive (retired) equipment is hidden from the listing.</summary>
    public bool IncludeInactive { get; init; }
}

/// <summary>
/// Flat read row for the equipment table (card [E4] #27). Enxuto by design: it exposes primitives the UI renders
/// directly — identity, asset tag, operational status, storage location, next-calibration date and its derived
/// calibration status — and never leaks the <c>Equipment</c> aggregate or its value objects.
/// </summary>
public sealed record EquipmentListItem(
    Guid Id,
    string Name,
    string AssetTag,
    EquipmentStatus Status,
    Guid? StorageLocationId,
    string? StorageLocationName,
    DateOnly? NextCalibrationDate,
    CalibrationStatus CalibrationStatus,
    bool IsActive);

internal sealed class ListEquipmentQueryHandler
    : BaseDataAccess, IQueryHandler<ListEquipmentQuery, PagedResult<EquipmentListItem>>
{
    // The CASE deriving calibration_status is a faithful mirror of CalibrationStatusRule.Classify: NotRequired
    // when there is no planned next_calibration ("n/a"); past that date => Overdue; within the @DueSoonWindowDays
    // window from @Today => DueSoon; otherwise UpToDate. Each branch returns the matching CalibrationStatus
    // ordinal so Dapper maps the column straight to the enum. is_active mirrors "status <> Inactive"; the status
    // itself is stored as the enum name, so it is compared against the @Inactive*/filter string params. @Today
    // comes from the handler (IClock), never the DB clock. company_id keeps the mandatory tenant scoping.
    private const string Sql =
        """
        WITH records AS (
            SELECT
                e.id,
                e.name,
                e.asset_tag,
                e.status,
                e.storage_location_id,
                l.name AS storage_location_name,
                e.next_calibration,
                CASE
                    WHEN e.next_calibration IS NULL THEN @NotRequired
                    WHEN e.next_calibration < @Today THEN @Overdue
                    WHEN e.next_calibration
                         <= (@Today + (@DueSoonWindowDays || ' days')::interval)::date THEN @DueSoon
                    ELSE @UpToDate
                END AS calibration_status,
                (e.status <> @InactiveStatus) AS is_active,
                ROW_NUMBER() OVER (ORDER BY e.name ASC, e.id ASC) AS row_number,
                (COUNT(*)    OVER ())::int                        AS total_rows
            FROM inventory.equipment AS e
            LEFT JOIN inventory.storage_locations AS l
                   ON l.id = e.storage_location_id
                  AND l.company_id = @CompanyId
            WHERE e.company_id = @CompanyId
              AND (@IncludeInactive OR e.status <> @InactiveStatus)
              AND (@StorageLocationId IS NULL OR e.storage_location_id = @StorageLocationId)
              AND (
                    @Status IS NULL
                    OR (CASE
                            WHEN e.next_calibration IS NULL THEN @NotRequired
                            WHEN e.next_calibration < @Today THEN @Overdue
                            WHEN e.next_calibration
                                 <= (@Today + (@DueSoonWindowDays || ' days')::interval)::date THEN @DueSoon
                            ELSE @UpToDate
                        END) = @Status
                  )
              AND (
                    @Search IS NULL
                    OR e.name ILIKE '%' || @Search || '%'
                    OR e.asset_tag ILIKE '%' || @Search || '%'
                  )
        )
        SELECT
            id,
            name,
            asset_tag             AS assettag,
            status,
            storage_location_id   AS storagelocationid,
            storage_location_name AS storagelocationname,
            next_calibration      AS nextcalibrationdate,
            calibration_status    AS calibrationstatus,
            is_active             AS isactive,
            total_rows            AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ListEquipmentQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<PagedResult<EquipmentListItem>> HandleAsync(
        ListEquipmentQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        EquipmentListQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<EquipmentListRow> rows = (await connection.QueryAsync<EquipmentListRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<EquipmentListItem> items = rows
            .Select(row => row.ToListItem())
            .ToList();

        return new PagedResult<EquipmentListItem>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), a blank search collapses to null, <c>@Today</c> comes
    /// from the injected <see cref="IClock"/> and the calibration-status ordinals mirror the SQL CASE — extracted
    /// so the tenant guard, the derived status and filter normalization are unit-testable without a live database.
    /// </summary>
    internal EquipmentListQueryParameters BuildParameters(ListEquipmentQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        DueSoonWindowDays: CalibrationStatusRule.DefaultDueSoonWindowDays,
        InactiveStatus: EquipmentStatus.Inactive.ToString(),
        Status: request.Status is { } status ? (int)status : null,
        StorageLocationId: request.StorageLocationId,
        Search: NormalizeFilter(request.Search),
        IncludeInactive: request.IncludeInactive,
        NotRequired: (int)CalibrationStatus.NotRequired,
        UpToDate: (int)CalibrationStatus.UpToDate,
        DueSoon: (int)CalibrationStatus.DueSoon,
        Overdue: (int)CalibrationStatus.Overdue,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>Trims a filter and collapses a blank value to null, so an empty box means "no filter".</summary>
    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>) alongside
    /// the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record EquipmentListRow(
        Guid Id,
        string Name,
        string AssetTag,
        EquipmentStatus Status,
        Guid? StorageLocationId,
        string? StorageLocationName,
        DateOnly? NextCalibrationDate,
        CalibrationStatus CalibrationStatus,
        bool IsActive,
        int TotalRows)
    {
        public EquipmentListItem ToListItem() => new(
            Id,
            Name,
            AssetTag,
            Status,
            StorageLocationId,
            StorageLocationName,
            NextCalibrationDate,
            CalibrationStatus,
            IsActive);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListEquipmentQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard, the derived calibration status and filter normalization can be asserted without a live database.
/// </summary>
internal sealed record EquipmentListQueryParameters(
    Guid CompanyId,
    DateOnly Today,
    int DueSoonWindowDays,
    string InactiveStatus,
    int? Status,
    Guid? StorageLocationId,
    string? Search,
    bool IncludeInactive,
    int NotRequired,
    int UpToDate,
    int DueSoon,
    int Overdue,
    int FirstResult,
    int LastResult);
