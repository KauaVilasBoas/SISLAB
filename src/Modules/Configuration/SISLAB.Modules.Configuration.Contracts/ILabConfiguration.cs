namespace SISLAB.Modules.Configuration.Contracts;

/// <summary>
/// Public boundary of the Configuration module (card [E12] #76): the <b>only</b> surface other modules may
/// depend on to read a tenant's laboratory configuration. Every member returns primitives-only contracts
/// owned here — never the internal aggregates (<c>ExpiryPolicy</c>, <c>ItemCategory</c>, …) or any EF type —
/// so consuming modules stay decoupled from the Configuration Domain/Application/Infrastructure (module
/// isolation, section 2; enforced by the architecture tests). Modelled after <c>IInventoryApi</c> (card
/// [E5] #35).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> Every operation is implicitly scoped to the active company. The <c>CompanyId</c>
/// is resolved by the adapter from <c>ITenantContext</c>, never passed by the caller — a consuming module
/// cannot read another tenant's configuration through this surface (defense-in-depth, section 7).
/// </para>
/// <para>
/// <b>Read-only.</b> This is a query surface. Configuration is changed through the module's own commands;
/// there is no cross-module mutation entry point by design (CQRS, section 2).
/// </para>
/// </remarks>
public interface ILabConfiguration
{
    /// <summary>
    /// Returns the active company's expiry warning window (in days) — how many days ahead of a batch's last
    /// valid day stock is flagged as "expiring soon". When the tenant has no configured policy yet, returns
    /// the sensible default so callers always get a usable value (the Inventory read-side uses it as the
    /// classification window, replacing its retired 30-day constant).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<int> GetExpiryWarningWindowDaysAsync(CancellationToken ct);

    /// <summary>
    /// Returns the active company's item category with <paramref name="categoryId"/>, or
    /// <see langword="null"/> when no such category exists for that company. Used by the Inventory write-side
    /// to resolve a category's name/controlled flag and to reject an unknown category.
    /// </summary>
    /// <param name="categoryId">Identifier of the category to load.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ItemCategoryDto?> GetItemCategoryAsync(Guid categoryId, CancellationToken ct);

    /// <summary>
    /// Returns <see langword="true"/> when an item category with <paramref name="categoryId"/> exists for the
    /// active company; otherwise <see langword="false"/>. Cheaper than <see cref="GetItemCategoryAsync"/> when
    /// only existence (the write-side validation guard) matters.
    /// </summary>
    /// <param name="categoryId">Identifier of the category to probe.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ItemCategoryExistsAsync(Guid categoryId, CancellationToken ct);

    /// <summary>
    /// Returns the active company's experimental model with <paramref name="modelId"/> (SISLAB-04), or
    /// <see langword="null"/> when no such model exists for that company. Used by the Experiments write-side to
    /// resolve a model when binding a batch ("leva") to it, and by the read-side to surface the bound model's
    /// name/timepoints/parameters on the project detail.
    /// </summary>
    /// <param name="modelId">Identifier of the experimental model to load.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ExperimentalModelDto?> GetExperimentalModelAsync(Guid modelId, CancellationToken ct);

    /// <summary>
    /// Returns <see langword="true"/> when an experimental model with <paramref name="modelId"/> exists for the
    /// active company; otherwise <see langword="false"/>. This is the write-side validation guard the Experiments
    /// module uses to reject binding a batch to an unknown/other-tenant model — cheaper than
    /// <see cref="GetExperimentalModelAsync"/> when only existence matters.
    /// </summary>
    /// <param name="modelId">Identifier of the experimental model to probe.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ExperimentalModelExistsAsync(Guid modelId, CancellationToken ct);

    /// <summary>
    /// Returns the active company's animal-inclusion criteria (SISLAB-02) — the cadastered "(parameter, operator,
    /// threshold, unit)" rules the Experiments module applies to select animals from their physiological readings.
    /// Never empty-guarantees a criterion for any parameter: a lab that has cadastered none gets an empty list, and a
    /// parameter without a criterion simply drives no inclusion decision.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<InclusionCriterionDto>> GetInclusionCriteriaAsync(CancellationToken ct);
}
