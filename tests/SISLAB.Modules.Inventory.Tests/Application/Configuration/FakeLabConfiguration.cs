using SISLAB.Modules.Configuration.Contracts;

namespace SISLAB.Modules.Inventory.Tests.Application.Configuration;

/// <summary>
/// In-memory <see cref="ILabConfiguration"/> test double for the Inventory handlers that read a tenant's lab
/// configuration across the Configuration boundary (card [E12] #76). It seeds a fixed expiry warning window
/// and a set of known category ids, so the write-side category guard and the read-side window resolution are
/// exercised without a live Configuration round-trip. Modelled on the module's other local fakes
/// (<c>FakeStockItemRepository</c>, <c>FakePartnerRepository</c>).
/// </summary>
internal sealed class FakeLabConfiguration : ILabConfiguration
{
    private readonly Dictionary<Guid, ItemCategoryDto> _categories = new();

    /// <summary>The expiry warning window returned by <see cref="GetExpiryWarningWindowDaysAsync"/>.</summary>
    public int WarningWindowDays { get; init; } = 30;

    /// <summary>Registers a known category, so the write-side existence guard accepts its id.</summary>
    public FakeLabConfiguration WithCategory(ItemCategoryDto category)
    {
        _categories[category.Id] = category;
        return this;
    }

    /// <summary>Registers a known category by id with a canonical name, for the common test path.</summary>
    public FakeLabConfiguration WithCategory(Guid id, string name = "Solvent", bool isControlled = false)
        => WithCategory(new ItemCategoryDto(id, name, isControlled));

    public Task<int> GetExpiryWarningWindowDaysAsync(CancellationToken ct)
        => Task.FromResult(WarningWindowDays);

    public Task<ItemCategoryDto?> GetItemCategoryAsync(Guid categoryId, CancellationToken ct)
        => Task.FromResult(_categories.GetValueOrDefault(categoryId));

    public Task<bool> ItemCategoryExistsAsync(Guid categoryId, CancellationToken ct)
        => Task.FromResult(_categories.ContainsKey(categoryId));
}
