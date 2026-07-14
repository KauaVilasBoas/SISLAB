namespace SISLAB.Modules.Configuration.Domain.ItemCategories;

/// <summary>
/// Repository for <see cref="ItemCategory"/> aggregates (interface in the Domain, implementation in the
/// Infrastructure). Reads are implicitly tenant-scoped by the write-side global query filter.
/// </summary>
public interface IItemCategoryRepository
{
    /// <summary>Returns the category with <paramref name="id"/> for the active company, or <see langword="null"/>.</summary>
    Task<ItemCategory?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the active company's category whose canonical name matches (case-insensitively), or null.</summary>
    Task<ItemCategory?> FindByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Adds a new category for the active company.</summary>
    Task AddAsync(ItemCategory category, CancellationToken ct = default);

    /// <summary>Marks an existing category as modified so the unit of work persists the change.</summary>
    Task UpdateAsync(ItemCategory category, CancellationToken ct = default);
}
