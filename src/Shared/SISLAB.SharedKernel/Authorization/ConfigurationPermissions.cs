namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for the Configuration bounded context (per-tenant reference data).
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
///
/// <para>Configuration is split into one controller per reference-data type; the write action of each
/// is permission-gated. These are administrative settings, so only privileged roles receive them.</para>
/// </summary>
public static class ConfigurationPermissions
{
    /// <summary>Write permission on <c>UnitController</c>.</summary>
    public const string UnitCreate = "Unit.Create";

    /// <summary>Write permission on <c>RoomController</c>.</summary>
    public const string RoomCreate = "Room.Create";

    /// <summary>Write permission on <c>ReferenceRangeController</c>.</summary>
    public const string ReferenceRangeCreate = "ReferenceRange.Create";

    /// <summary>Write permission on <c>ItemCategoryController</c>.</summary>
    public const string ItemCategoryCreate = "ItemCategory.Create";

    /// <summary>Write permission on <c>ExpiryPolicyController</c>.</summary>
    public const string ExpiryPolicySetWarningWindow = "ExpiryPolicy.SetWarningWindow";

    /// <summary>Every Configuration write permission.</summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>
    {
        UnitCreate, RoomCreate, ReferenceRangeCreate, ItemCategoryCreate, ExpiryPolicySetWarningWindow
    };
}
