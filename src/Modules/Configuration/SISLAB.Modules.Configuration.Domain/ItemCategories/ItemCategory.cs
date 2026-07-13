using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.ItemCategories;

/// <summary>
/// A configurable, per-tenant category for stock items (card [E12] #76). This aggregate is the dynamic
/// replacement for the closed <c>StockItemCategory</c> enum that used to live in the Inventory domain: a
/// laboratory now defines its own categories instead of being locked to a fixed list, and the Inventory
/// module references a category only <b>by value</b> (its <see cref="Entity{TId}.Id"/>), validating it
/// through the module's public <c>ILabConfiguration</c> port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Name + aliases + controlled flag.</b> A category has a canonical <see cref="Name"/>, a set of
/// <see cref="Aliases"/> (apelidos used by imports/UI to resolve free-typed names) and an
/// <see cref="IsControlled"/> flag marking categories whose items are controlled substances (anesthetics,
/// opioids). The controlled flag lives here so a lab can declare "everything in this category is controlled"
/// declaratively; the per-item controlled flag in Inventory remains independent (an item can be controlled
/// regardless of its category), preserving the E3 #21 decision.
/// </para>
/// <para>
/// <b>Deterministic seeding.</b> The nine legacy enum values are seeded per company through
/// <see cref="Seed"/> with a deterministic id derived from <c>(company_id, canonical code)</c>, so the
/// migration's data backfill (enum string → category_id) is reproducible and idempotent.
/// </para>
/// </remarks>
public sealed class ItemCategory : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 120;

    // Parameterless constructor for EF Core materialization.
    private ItemCategory() : base(Guid.Empty) => Aliases = CategoryAliases.None;

    private ItemCategory(Guid id, string name, CategoryAliases aliases, bool isControlled) : base(id)
    {
        Name = name;
        Aliases = aliases;
        IsControlled = isControlled;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Canonical, human-readable category name (unique per tenant, enforced by a unique index).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Alternative names the lab uses for this category (imports/UI resolution).</summary>
    public CategoryAliases Aliases { get; private set; }

    /// <summary>When true, items in this category are controlled substances requiring extra traceability.</summary>
    public bool IsControlled { get; private set; }

    /// <summary>Creates a new tenant category with a validated name and (optional) aliases.</summary>
    public static ItemCategory Create(string name, IEnumerable<string>? aliases = null, bool isControlled = false)
        => new(Guid.NewGuid(), NormalizeName(name), CategoryAliases.From(aliases), isControlled);

    /// <summary>
    /// Rehydrates a category at a caller-supplied deterministic id — used by the tenant seeder and the
    /// enum→category data migration so re-runs never duplicate a category for the same company.
    /// </summary>
    internal static ItemCategory Seed(Guid id, string name, IEnumerable<string>? aliases, bool isControlled)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        return new ItemCategory(id, NormalizeName(name), CategoryAliases.From(aliases), isControlled);
    }

    /// <summary>Renames the category (still unique per tenant), keeping the same identity.</summary>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Replaces the category's alias set with a normalized value.</summary>
    public void ChangeAliases(IEnumerable<string>? aliases) => Aliases = CategoryAliases.From(aliases);

    /// <summary>Marks or unmarks the category as controlled.</summary>
    public void SetControlled(bool isControlled) => IsControlled = isControlled;

    private static string NormalizeName(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));
        return trimmed;
    }
}
