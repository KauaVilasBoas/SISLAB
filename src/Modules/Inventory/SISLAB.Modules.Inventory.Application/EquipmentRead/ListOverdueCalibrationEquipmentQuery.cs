using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.EquipmentRead;

/// <summary>
/// Read-side query (card [E6] #66) that lists the equipment of the <b>active company</b> whose calibration is
/// <b>overdue</b> — the next calibration date has already passed — for the E6 overdue-calibration alert job.
/// It reads the <c>inventory.equipment</c> table directly via Dapper (no read-model materialization needed:
/// the overdue state is derived in SQL from <c>next_calibration</c>), projecting the flat
/// <see cref="OverdueCalibrationEquipment"/> the job needs, ordered by how long calibration has been overdue
/// (most overdue first).
/// </summary>
/// <remarks>
/// <para>
/// <b>Overdue rule.</b> An equipment is listed when <c>next_calibration &lt; @Today</c>. Equipment with a
/// null <c>next_calibration</c> — calibration not applicable, or recorded without a planned next date — is
/// never listed; this mirrors the domain rule <c>CalibrationSchedule.IsOverdue</c> ("an equipment with no
/// planned next date is never overdue") and honours the card's "ignora os n/a". The status is <b>derived in
/// the SQL</b> (a constant <c>Overdue</c> discriminator for every returned row), never materialized on the table.
/// </para>
/// <para>
/// <b>Days overdue.</b> <c>@Today − next_calibration</c>, always positive for a listed row, computed against
/// the handler-supplied <c>@Today</c> (from <see cref="IClock"/>), never the database clock.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler (the E6 job
/// sets it per company via the tenant-override seam), never from the request, and the SELECT keeps
/// <c>WHERE company_id = @CompanyId</c> — the read side has no EF global query filter (defense-in-depth, section 7).
/// </para>
/// </remarks>
public sealed record ListOverdueCalibrationEquipmentQuery
    : PagedQuery<PagedResult<OverdueCalibrationEquipment>>;

/// <summary>Derived calibration status exposed by the overdue-calibration read query (#66).</summary>
public enum CalibrationStatusView
{
    /// <summary>The next calibration date has already passed ("calibração atrasada").</summary>
    Overdue = 1
}

/// <summary>
/// Flat read row for the overdue-calibration alert job (card [E6] #66). Enxuto by design: it exposes the
/// equipment identity, its asset tag, the last/next calibration dates, the derived status and the signed
/// days-overdue — never the <c>Equipment</c> aggregate or its value objects.
/// </summary>
public sealed record OverdueCalibrationEquipment(
    Guid Id,
    string Name,
    string AssetTag,
    string? Brand,
    string? Model,
    DateOnly? LastCalibration,
    DateOnly NextCalibration,
    CalibrationStatusView CalibrationStatus,
    int DaysOverdue,
    Guid? StorageLocationId);

internal sealed class ListOverdueCalibrationEquipmentQueryHandler
    : BaseDataAccess, IQueryHandler<ListOverdueCalibrationEquipmentQuery, PagedResult<OverdueCalibrationEquipment>>
{
    // Overdue = next_calibration strictly before @Today; NULL next_calibration is pruned by the WHERE, so
    // "n/a" and "no planned next date" equipment never appear (mirrors CalibrationSchedule.IsOverdue). The
    // status is derived in SQL as the Overdue discriminator (@Overdue) for every returned row, never stored.
    // days_overdue = @Today − next_calibration, driving the ordering (most overdue first). company_id keeps
    // the mandatory tenant scoping (read side has no EF query filter).
    private const string Sql =
        """
        WITH records AS (
            SELECT
                e.id,
                e.name,
                e.asset_tag,
                e.brand,
                e.model,
                e.last_calibration,
                e.next_calibration,
                @Overdue AS calibration_status,
                (@Today - e.next_calibration) AS days_overdue,
                e.storage_location_id,
                ROW_NUMBER() OVER (ORDER BY e.next_calibration ASC, e.name ASC, e.id ASC) AS row_number,
                (COUNT(*)    OVER ())::int                                                AS total_rows
            FROM inventory.equipment AS e
            WHERE e.company_id = @CompanyId
              AND e.next_calibration IS NOT NULL
              AND e.next_calibration < @Today
        )
        SELECT
            id,
            name,
            asset_tag           AS assettag,
            brand,
            model,
            last_calibration    AS lastcalibration,
            next_calibration    AS nextcalibration,
            calibration_status  AS calibrationstatus,
            days_overdue        AS daysoverdue,
            storage_location_id AS storagelocationid,
            total_rows          AS totalrows
        FROM records
        WHERE row_number BETWEEN @FirstResult AND @LastResult
        ORDER BY row_number;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public ListOverdueCalibrationEquipmentQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<PagedResult<OverdueCalibrationEquipment>> HandleAsync(
        ListOverdueCalibrationEquipmentQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        OverdueCalibrationQueryParameters parameters = BuildParameters(request);

        IReadOnlyList<OverdueCalibrationEquipmentRow> rows = (await connection.QueryAsync<OverdueCalibrationEquipmentRow>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken))).AsList();

        int totalCount = rows.Count > 0 ? rows[0].TotalRows : 0;

        IReadOnlyList<OverdueCalibrationEquipment> items = rows
            .Select(row => row.ToOverdueCalibrationEquipment())
            .ToList();

        return new PagedResult<OverdueCalibrationEquipment>(items, totalCount, request.Page, request.PageSize);
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request) and <c>@Today</c> from the injected <see cref="IClock"/> —
    /// extracted so the tenant guard and pagination bounds are unit-testable without a live database.
    /// </summary>
    internal OverdueCalibrationQueryParameters BuildParameters(ListOverdueCalibrationEquipmentQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        Overdue: (int)CalibrationStatusView.Overdue,
        FirstResult: request.FirstResult,
        LastResult: request.LastResult);

    /// <summary>
    /// Dapper materialization row: carries the per-page <c>total_rows</c> (from <c>COUNT(*) OVER()</c>)
    /// alongside the projected columns, so the total and the page come back in a single round-trip.
    /// </summary>
    private sealed record OverdueCalibrationEquipmentRow(
        Guid Id,
        string Name,
        string AssetTag,
        string? Brand,
        string? Model,
        DateOnly? LastCalibration,
        DateOnly NextCalibration,
        CalibrationStatusView CalibrationStatus,
        int DaysOverdue,
        Guid? StorageLocationId,
        int TotalRows)
    {
        public OverdueCalibrationEquipment ToOverdueCalibrationEquipment() => new(
            Id,
            Name,
            AssetTag,
            Brand,
            Model,
            LastCalibration,
            NextCalibration,
            CalibrationStatus,
            DaysOverdue,
            StorageLocationId);
    }
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="ListOverdueCalibrationEquipmentQuery"/>. The property names
/// match the <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests
/// so the tenant guard, the derived status and pagination can be asserted without a live database.
/// </summary>
internal sealed record OverdueCalibrationQueryParameters(
    Guid CompanyId,
    DateOnly Today,
    int Overdue,
    int FirstResult,
    int LastResult);
