using SISLAB.Modules.Inventory.Domain.Equipments.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Modules.Inventory.Domain.Equipments;

/// <summary>
/// A piece of laboratory equipment the company owns and operates (a plate reader, a centrifuge, a
/// -80 °C freezer, an autoclave, a vortex, ...). The aggregate owns the equipment's identification
/// (asset tag, brand/model), its operational <see cref="EquipmentStatus"/> and the invariants that
/// govern how it moves between statuses, its optional calibration schedule and its append-only
/// maintenance history (screen "Equipamentos" #48, card [E3] #27).
/// </summary>
/// <remarks>
/// <para>
/// Status transitions are a domain rule, not free-form assignment: only the moves declared in
/// <see cref="AllowedTransitions"/> are accepted, so the equipment can never reach an incoherent state
/// (for example jumping straight from maintenance into use). The policy lives with the aggregate that
/// owns the status.
/// </para>
/// <para>
/// Calibration is optional and modelled by the presence of a <see cref="CalibrationSchedule"/>: a
/// <see langword="null"/> schedule means calibration is not applicable (n/a — e.g. a vortex). The
/// derived "overdue" state ("calibração atrasada") is computed on demand from the schedule and a clock
/// via <see cref="IsCalibrationOverdue"/>; it is never persisted. The periodic scan and the alert belong
/// to the E6 job, not to this aggregate.
/// </para>
/// <para>
/// Responsible-party decision (card [E3] #27, option A): a <see cref="MaintenanceRecord"/> intentionally
/// does not carry a "responsible user". <i>Who</i> performed/logged the maintenance is audit-trail data
/// owned by the audit card ([E9] #57) — mirroring how the stock-entry aggregate left the "who" to the
/// audit trail on card [E3] #24. Inventory has no access to the logged-in user id (<c>IUserIdAccessor</c>
/// is a Lumen/Identity concern; <c>ITenantContext</c> exposes only the <c>CompanyId</c>), so anticipating
/// it here would contradict #24 and break module isolation.
/// </para>
/// <para>
/// The storage location is referenced by value (<see cref="StorageLocationId"/>) and is optional; the
/// aggregate does not navigate to, nor know the type of, the location — there is no cross-aggregate
/// relationship.
/// </para>
/// </remarks>
public sealed class Equipment : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 200;
    private const int MaxAssetTagLength = 60;
    private const int MaxBrandLength = 120;
    private const int MaxModelLength = 120;
    private const int MaxMaintenanceRecords = 500;

    /// <summary>
    /// The only status moves the domain accepts. An equipment leaves maintenance or inactivity through
    /// <see cref="EquipmentStatus.Available"/> (it becomes ready again) rather than jumping straight into
    /// use; from there it can be put in use. This keeps the lifecycle coherent for the lab operator.
    /// </summary>
    private static readonly IReadOnlyDictionary<EquipmentStatus, IReadOnlySet<EquipmentStatus>>
        AllowedTransitions = new Dictionary<EquipmentStatus, IReadOnlySet<EquipmentStatus>>
        {
            [EquipmentStatus.Available] = new HashSet<EquipmentStatus>
            {
                EquipmentStatus.InUse,
                EquipmentStatus.UnderMaintenance,
                EquipmentStatus.Inactive,
            },
            [EquipmentStatus.InUse] = new HashSet<EquipmentStatus>
            {
                EquipmentStatus.Available,
                EquipmentStatus.UnderMaintenance,
                EquipmentStatus.Inactive,
            },
            [EquipmentStatus.UnderMaintenance] = new HashSet<EquipmentStatus>
            {
                EquipmentStatus.Available,
                EquipmentStatus.Inactive,
            },
            [EquipmentStatus.Inactive] = new HashSet<EquipmentStatus>
            {
                EquipmentStatus.Available,
            },
        };

    private readonly List<MaintenanceRecord> _maintenanceRecords = [];

    // Parameterless constructor for EF Core materialization.
    private Equipment() : base(Guid.Empty) { }

    private Equipment(
        Guid id,
        string name,
        string assetTag,
        string? brand,
        string? model,
        Guid? storageLocationId,
        EquipmentStatus status,
        CalibrationSchedule? calibration)
        : base(id)
    {
        Name = name;
        AssetTag = assetTag;
        Brand = brand;
        Model = model;
        StorageLocationId = storageLocationId;
        Status = status;
        Calibration = calibration;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    public string Name { get; private set; } = default!;

    /// <summary>Asset/patrimony tag identifying the physical unit (e.g. "PAT-0041").</summary>
    public string AssetTag { get; private set; } = default!;

    /// <summary>Manufacturer/brand (e.g. "BioTek"); optional.</summary>
    public string? Brand { get; private set; }

    /// <summary>Model designation (e.g. "Synergy H1"); optional.</summary>
    public string? Model { get; private set; }

    /// <summary>Storage location that holds the equipment, referenced by value; optional.</summary>
    public Guid? StorageLocationId { get; private set; }

    public EquipmentStatus Status { get; private set; }

    /// <summary>Calibration schedule; <see langword="null"/> when calibration does not apply (n/a).</summary>
    public CalibrationSchedule? Calibration { get; private set; }

    /// <summary>Append-only maintenance history of this equipment.</summary>
    public IReadOnlyList<MaintenanceRecord> MaintenanceRecords => _maintenanceRecords.AsReadOnly();

    /// <summary>True while the equipment is retired/decommissioned.</summary>
    public bool IsInactive => Status == EquipmentStatus.Inactive;

    /// <summary>
    /// Registers a new equipment. It starts <see cref="EquipmentStatus.Available"/> by default and may
    /// carry an initial calibration schedule; passing none leaves calibration as not applicable (n/a).
    /// </summary>
    public static Equipment Register(
        string name,
        string assetTag,
        string? brand = null,
        string? model = null,
        Guid? storageLocationId = null,
        EquipmentStatus status = EquipmentStatus.Available,
        CalibrationSchedule? calibration = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Guard.AgainstNullOrWhiteSpace(assetTag, nameof(assetTag));
        string trimmedAssetTag = assetTag.Trim();
        Guard.AgainstMaxLength(trimmedAssetTag, MaxAssetTagLength, nameof(assetTag));

        var equipment = new Equipment(
            Guid.NewGuid(),
            trimmedName,
            trimmedAssetTag,
            NormalizeOptionalText(brand, MaxBrandLength, nameof(brand)),
            NormalizeOptionalText(model, MaxModelLength, nameof(model)),
            NormalizeStorageLocation(storageLocationId),
            status,
            calibration);

        equipment.RaiseDomainEvent(
            new EquipmentRegisteredEvent(equipment.Id, equipment.Name, equipment.AssetTag));
        return equipment;
    }

    /// <summary>Renames the equipment.</summary>
    public void Rename(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Name = trimmedName;
    }

    /// <summary>Updates the asset/patrimony tag.</summary>
    public void ReassignAssetTag(string assetTag)
    {
        Guard.AgainstNullOrWhiteSpace(assetTag, nameof(assetTag));
        string trimmedAssetTag = assetTag.Trim();
        Guard.AgainstMaxLength(trimmedAssetTag, MaxAssetTagLength, nameof(assetTag));

        AssetTag = trimmedAssetTag;
    }

    /// <summary>Updates the brand and model; passing null or blank clears the corresponding field.</summary>
    public void DescribeModel(string? brand, string? model)
    {
        Brand = NormalizeOptionalText(brand, MaxBrandLength, nameof(brand));
        Model = NormalizeOptionalText(model, MaxModelLength, nameof(model));
    }

    /// <summary>Moves the equipment to another storage location; passing null clears the location.</summary>
    public void RelocateTo(Guid? storageLocationId)
        => StorageLocationId = NormalizeStorageLocation(storageLocationId);

    /// <summary>Whether the equipment may move to <paramref name="target"/> from its current status.</summary>
    public bool CanChangeStatusTo(EquipmentStatus target)
        => target != Status && AllowedTransitions[Status].Contains(target);

    /// <summary>
    /// Moves the equipment to a new operational status, enforcing the transition policy. Moving to the
    /// current status is a no-op; an unsupported move is rejected.
    /// </summary>
    public void ChangeStatus(EquipmentStatus target)
    {
        if (target == Status)
            return;

        if (!AllowedTransitions[Status].Contains(target))
            throw new DomainException(
                $"Equipment '{Name}' cannot move from {Status} to {target}.");

        EquipmentStatus previous = Status;
        Status = target;

        RaiseDomainEvent(new EquipmentStatusChangedEvent(Id, previous, target));
    }

    /// <summary>Defines or replaces the calibration schedule; passing null marks calibration as n/a.</summary>
    public void DefineCalibration(CalibrationSchedule? calibration) => Calibration = calibration;

    /// <summary>
    /// True when a calibration is due and its date has already passed relative to the supplied clock (the
    /// "calibração atrasada" state). Equipment with no applicable calibration (n/a) is never overdue.
    /// Derived on demand; never persisted.
    /// </summary>
    public bool IsCalibrationOverdue(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return Calibration?.IsOverdue(clock) ?? false;
    }

    /// <summary>
    /// Logs a maintenance event into the equipment's append-only history. Does not change the equipment
    /// status; moving in/out of maintenance is an explicit <see cref="ChangeStatus"/> decision.
    /// </summary>
    public void RecordMaintenance(MaintenanceRecord record)
    {
        Guard.AgainstNull(record, nameof(record));

        if (_maintenanceRecords.Count >= MaxMaintenanceRecords)
            throw new DomainException(
                $"Equipment '{Name}' already has the maximum of {MaxMaintenanceRecords} maintenance records.");

        _maintenanceRecords.Add(record);

        RaiseDomainEvent(new EquipmentMaintenanceRecordedEvent(Id, record.Date, record.Type));
    }

    private static Guid? NormalizeStorageLocation(Guid? storageLocationId)
        => storageLocationId is { } id && id != Guid.Empty ? id : null;

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
