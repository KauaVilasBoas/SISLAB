using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.StorageLocations.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Domain.StorageLocations;

/// <summary>
/// A place where the laboratory keeps stock (a warehouse, a reagent cabinet, a freezer, a controlled
/// box, a partner shelf). The aggregate owns the storage rules that depend on the location's type and
/// status, which the <see cref="StockItem"/> aggregate cannot enforce on its own because it references
/// locations only by value and does not know their type.
/// </summary>
/// <remarks>
/// <para>
/// Controlled-storage invariant: a controlled item may only reside in a <see cref="StorageLocationType.Controlled"/>
/// location. The <see cref="StockItem"/> transfer method (card [E3] #21) deliberately left this out
/// because it lacks the destination's type. The transfer handler (card [E3] #26) orchestrates both
/// aggregates and calls <see cref="EnsureCanStore(StockItem)"/> on the destination before moving the
/// item, so the rule lives with the aggregate that owns the knowledge.
/// </para>
/// <para>
/// Temperature range: only a <see cref="StorageLocationType.Refrigerated"/> location may declare a
/// <see cref="TemperatureRange"/> (decision recorded on card [E3] #23). It is the conservation target of
/// the location itself, independent of any Equipment record; live device monitoring belongs to the
/// Equipment module. Item counts and criticality shown per location in the UI are read-side derivations
/// (card [E4] #29), never persisted here.
/// </para>
/// </remarks>
public sealed class StorageLocation : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 150;
    private const int MaxDescriptionLength = 500;

    // Parameterless constructor for EF Core materialization.
    private StorageLocation() : base(Guid.Empty) { }

    private StorageLocation(
        Guid id,
        string name,
        StorageLocationType type,
        string? description,
        TemperatureRange? temperatureRange)
        : base(id)
    {
        Name = name;
        Type = type;
        Description = description;
        TemperatureRange = temperatureRange;
        IsActive = true;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    public string Name { get; private set; } = default!;

    public StorageLocationType Type { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Target conservation range; only ever set for a <see cref="StorageLocationType.Refrigerated"/> location.</summary>
    public TemperatureRange? TemperatureRange { get; private set; }

    /// <summary>Whether the location is in service. An inactive location cannot receive stock.</summary>
    public bool IsActive { get; private set; }

    /// <summary>True for the only type allowed to hold controlled items.</summary>
    public bool IsControlledStorage => Type == StorageLocationType.Controlled;

    /// <summary>
    /// Registers a new storage location. A temperature range may only be supplied for a refrigerated
    /// location; supplying one for any other type is rejected.
    /// </summary>
    public static StorageLocation Register(
        string name,
        StorageLocationType type,
        string? description = null,
        TemperatureRange? temperatureRange = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));
        EnsureTemperatureRangeMatchesType(type, temperatureRange);

        var location = new StorageLocation(
            Guid.NewGuid(),
            trimmedName,
            type,
            NormalizeDescription(description),
            temperatureRange);

        location.RaiseDomainEvent(new StorageLocationRegisteredEvent(location.Id, location.Name, location.Type));
        return location;
    }

    /// <summary>Renames the location, keeping the same type and status.</summary>
    public void Rename(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Name = trimmedName;
    }

    /// <summary>Updates the free-text description; passing null or blank clears it.</summary>
    public void DescribeAs(string? description) => Description = NormalizeDescription(description);

    /// <summary>
    /// Defines the target temperature range of a refrigerated location; passing null clears it. Fails
    /// for any non-refrigerated location.
    /// </summary>
    public void DefineTemperatureRange(TemperatureRange? temperatureRange)
    {
        EnsureTemperatureRangeMatchesType(Type, temperatureRange);
        TemperatureRange = temperatureRange;
    }

    /// <summary>Takes the location out of service. Idempotent.</summary>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        RaiseDomainEvent(new StorageLocationDeactivatedEvent(Id));
    }

    /// <summary>Puts a deactivated location back in service. Idempotent.</summary>
    public void Reactivate()
    {
        if (IsActive)
            return;

        IsActive = true;
        RaiseDomainEvent(new StorageLocationReactivatedEvent(Id));
    }

    /// <summary>
    /// True when this location may hold an item with the given controlled flag: controlled items are
    /// only accepted by a controlled location. Does not consider the active status; use
    /// <see cref="EnsureCanStore(StockItem)"/> for the full storage guard.
    /// </summary>
    public bool CanStore(bool isControlledItem) => !isControlledItem || IsControlledStorage;

    /// <summary>
    /// Guards that this location may receive the given item: the location must be active and, if the
    /// item is controlled, it must be a controlled location. Called by the transfer handler
    /// (card [E3] #26) on the destination before the item is moved.
    /// </summary>
    public void EnsureCanStore(StockItem item)
    {
        Guard.AgainstNull(item, nameof(item));

        if (!IsActive)
            throw new DomainException(
                $"Storage location '{Name}' is inactive and cannot receive stock.");

        if (!CanStore(item.IsControlled))
            throw new DomainException(
                $"Controlled item '{item.Name}' can only be stored in a controlled location; '{Name}' is a {Type} location.");
    }

    private static void EnsureTemperatureRangeMatchesType(
        StorageLocationType type,
        TemperatureRange? temperatureRange)
    {
        if (temperatureRange is not null && type != StorageLocationType.Refrigerated)
            throw new DomainException(
                $"A temperature range can only be defined for a refrigerated location, not a {type} one.");
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        string trimmed = description.Trim();
        Guard.AgainstMaxLength(trimmed, MaxDescriptionLength, nameof(description));
        return trimmed;
    }
}
