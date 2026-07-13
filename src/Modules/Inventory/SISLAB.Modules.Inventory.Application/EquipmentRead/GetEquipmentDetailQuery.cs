using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Inventory.Domain.Equipments;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Application.EquipmentRead;

/// <summary>
/// Read-side query (card [E4] #27) that loads the single equipment of the <b>active company</b> identified by
/// <see cref="EquipmentId"/>, or <see langword="null"/> when no such equipment exists for that company. It reads
/// the <c>inventory.equipment</c> table via Dapper (joined with <c>inventory.storage_locations</c> for the
/// location name and with <c>inventory.equipment_maintenances</c> for the last-maintenance date) — never the write
/// DbContext — and projects the flat <see cref="EquipmentDetail"/> the equipment detail panel needs.
/// </summary>
/// <remarks>
/// <para>
/// The detail extends the listing row with the equipment's identification (manufacturer/model), its last
/// calibration date and its most recent maintenance date. <c>manufacturer</c> maps onto the aggregate's
/// <c>brand</c>; <c>last_maintenance_date</c> is <c>MAX(equipment_maintenances.date)</c>, derived rather than
/// stored (the maintenance history is an owned child collection of the aggregate).
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from the
/// request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read side has no EF global query
/// filter (defense-in-depth, section 7). An id belonging to another company is indistinguishable from a
/// non-existent one: both yield <see langword="null"/>, which the controller maps to a 404.
/// </para>
/// </remarks>
public sealed record GetEquipmentDetailQuery(Guid EquipmentId) : IQuery<EquipmentDetail?>;

/// <summary>
/// Flat read row for a single equipment (card [E4] #27). Extends the listing projection with the identification
/// (manufacturer/model), the last calibration date and the derived last-maintenance date. Enxuto by design: it
/// never leaks the <c>Equipment</c> aggregate or its value objects.
/// </summary>
public sealed record EquipmentDetail(
    Guid Id,
    string Name,
    string AssetTag,
    EquipmentStatus Status,
    Guid? StorageLocationId,
    string? StorageLocationName,
    DateOnly? NextCalibrationDate,
    CalibrationStatus CalibrationStatus,
    bool IsActive,
    string? Manufacturer,
    string? Model,
    DateOnly? LastCalibrationDate,
    DateOnly? LastMaintenanceDate);

internal sealed class GetEquipmentDetailQueryHandler
    : BaseDataAccess, IQueryHandler<GetEquipmentDetailQuery, EquipmentDetail?>
{
    // Single-row lookup by (company_id, id): no pagination window is needed. The calibration_status CASE mirrors
    // CalibrationStatusRule.Classify (NotRequired/Overdue/DueSoon/UpToDate), is_active mirrors "status <> Inactive",
    // and last_maintenance_date is the MAX over the owned maintenance child rows (NULL when none). company_id keeps
    // the mandatory tenant scoping, so an id from another tenant returns no row (→ null), exactly like a missing id.
    // @Today comes from the handler (IClock), never the DB clock. Columns are aliased to the EquipmentDetail
    // property names (Dapper binds by name).
    private const string Sql =
        """
        SELECT
            e.id                     AS id,
            e.name                   AS name,
            e.asset_tag              AS assettag,
            e.status                 AS status,
            e.storage_location_id    AS storagelocationid,
            l.name                   AS storagelocationname,
            e.next_calibration       AS nextcalibrationdate,
            CASE
                WHEN e.next_calibration IS NULL THEN @NotRequired
                WHEN e.next_calibration < @Today THEN @Overdue
                WHEN e.next_calibration
                     <= (@Today + (@DueSoonWindowDays || ' days')::interval)::date THEN @DueSoon
                ELSE @UpToDate
            END                      AS calibrationstatus,
            (e.status <> @InactiveStatus) AS isactive,
            e.brand                  AS manufacturer,
            e.model                  AS model,
            e.last_calibration       AS lastcalibrationdate,
            (
                SELECT MAX(m.date)
                FROM inventory.equipment_maintenances AS m
                WHERE m.equipment_id = e.id
            )                        AS lastmaintenancedate
        FROM inventory.equipment AS e
        LEFT JOIN inventory.storage_locations AS l
               ON l.id = e.storage_location_id
              AND l.company_id = @CompanyId
        WHERE e.company_id = @CompanyId
          AND e.id = @EquipmentId;
        """;

    private readonly ITenantContext _tenantContext;
    private readonly IClock _clock;

    public GetEquipmentDetailQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        IClock clock)
        : base(connectionFactory)
    {
        _tenantContext = tenantContext;
        _clock = clock;
    }

    public async Task<EquipmentDetail?> HandleAsync(
        GetEquipmentDetailQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        EquipmentDetailQueryParameters parameters = BuildParameters(request);

        return await connection.QuerySingleOrDefaultAsync<EquipmentDetail>(
            new CommandDefinition(Sql, parameters, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Materializes the Dapper parameter set for <paramref name="request"/>. The company id always comes from
    /// <see cref="ITenantContext"/> (never the request), <c>@Today</c> from the injected <see cref="IClock"/> and
    /// the calibration-status ordinals mirror the SQL CASE — extracted so the tenant guard and the derived status
    /// are unit-testable without a live database.
    /// </summary>
    internal EquipmentDetailQueryParameters BuildParameters(GetEquipmentDetailQuery request) => new(
        CompanyId: _tenantContext.CompanyId,
        EquipmentId: request.EquipmentId,
        Today: DateOnly.FromDateTime(_clock.UtcNow),
        DueSoonWindowDays: CalibrationStatusRule.DefaultDueSoonWindowDays,
        InactiveStatus: EquipmentStatus.Inactive.ToString(),
        NotRequired: (int)CalibrationStatus.NotRequired,
        UpToDate: (int)CalibrationStatus.UpToDate,
        DueSoon: (int)CalibrationStatus.DueSoon,
        Overdue: (int)CalibrationStatus.Overdue);
}

/// <summary>
/// Immutable Dapper parameter set for <see cref="GetEquipmentDetailQuery"/>. The property names match the
/// <c>@Parameter</c> tokens in the SQL exactly (Dapper binds by name). Exposed to the module's tests so the
/// tenant guard and the derived calibration status can be asserted without a live database.
/// </summary>
internal sealed record EquipmentDetailQueryParameters(
    Guid CompanyId,
    Guid EquipmentId,
    DateOnly Today,
    int DueSoonWindowDays,
    string InactiveStatus,
    int NotRequired,
    int UpToDate,
    int DueSoon,
    int Overdue);
