namespace SISLAB.Modules.Configuration.Contracts;

/// <summary>
/// Public, flattened view of a tenant's item category, returned across the module boundary by
/// <see cref="ILabConfiguration"/>. It carries only primitives — never the internal <c>ItemCategory</c>
/// aggregate or its value objects — so a consuming module (Inventory) depends on nothing of the
/// Configuration Domain (module isolation, section 2).
/// </summary>
/// <param name="Id">Category identifier — the value the Inventory <c>StockItem</c> references by value.</param>
/// <param name="Name">Canonical category name.</param>
/// <param name="IsControlled">True when items in this category are controlled substances.</param>
public sealed record ItemCategoryDto(Guid Id, string Name, bool IsControlled);
