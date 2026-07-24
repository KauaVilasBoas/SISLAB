using SISLAB.Modules.Configuration.Application.ExperimentalModels;
using SISLAB.Modules.Configuration.Application.ExpiryPolicies;
using SISLAB.Modules.Configuration.Application.ItemCategories;
using SISLAB.Modules.Configuration.Contracts;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Configuration.Application.PublicApi;

/// <summary>
/// Adapter implementing the Configuration module's public boundary <see cref="ILabConfiguration"/> (card
/// [E12] #76). It is the single place that translates the module's internal read models (the E12 Dapper
/// queries) into the primitives-only contracts other modules consume — nothing of the Configuration
/// Domain/EF crosses the boundary. Mirrors the Inventory <c>InventoryApi</c> adapter (card [E5] #35).
/// </summary>
/// <remarks>
/// <para>
/// <b>Delegation, not re-implementation.</b> Every operation dispatches an existing read query through
/// <see cref="IMediator"/> and maps its result — there is no SQL here.
/// </para>
/// <para>
/// <b>Tenant scoping.</b> The adapter passes no company id: it lives inside the module, so the queries it
/// dispatches resolve the active company from <c>ITenantContext</c> themselves and keep the mandatory
/// <c>WHERE company_id = @CompanyId</c>. A caller cannot target another tenant through this surface.
/// </para>
/// </remarks>
internal sealed class LabConfiguration : ILabConfiguration
{
    private readonly IMediator _mediator;

    public LabConfiguration(IMediator mediator) => _mediator = mediator;

    /// <inheritdoc />
    public Task<int> GetExpiryWarningWindowDaysAsync(CancellationToken ct)
        => _mediator.SendAsync(new GetExpiryWarningWindowQuery(), ct);

    /// <inheritdoc />
    public async Task<ItemCategoryDto?> GetItemCategoryAsync(Guid categoryId, CancellationToken ct)
    {
        ItemCategoryView? view = await _mediator.SendAsync(new GetItemCategoryQuery(categoryId), ct);

        return view is null ? null : new ItemCategoryDto(view.Id, view.Name, view.IsControlled);
    }

    /// <inheritdoc />
    public async Task<bool> ItemCategoryExistsAsync(Guid categoryId, CancellationToken ct)
    {
        ItemCategoryView? view = await _mediator.SendAsync(new GetItemCategoryQuery(categoryId), ct);

        return view is not null;
    }

    /// <inheritdoc />
    public async Task<ExperimentalModelDto?> GetExperimentalModelAsync(Guid modelId, CancellationToken ct)
    {
        ExperimentalModelView? view = await _mediator.SendAsync(new GetExperimentalModelQuery(modelId), ct);

        return view is null ? null : MapToDto(view);
    }

    /// <inheritdoc />
    public async Task<bool> ExperimentalModelExistsAsync(Guid modelId, CancellationToken ct)
    {
        ExperimentalModelView? view = await _mediator.SendAsync(new GetExperimentalModelQuery(modelId), ct);

        return view is not null;
    }

    /// <summary>
    /// Translates the module's internal read view into the primitives-only Contracts DTO, so nothing of the
    /// Configuration Domain (the <c>StandardGroupKind</c> enum, the value objects) crosses the boundary — the kind
    /// is flattened to its stable string code.
    /// </summary>
    private static ExperimentalModelDto MapToDto(ExperimentalModelView view)
        => new(
            view.Id,
            view.Name,
            view.Description,
            new InductionProtocolDto(
                view.Induction.Administrations,
                view.Induction.IntervalDays,
                view.Induction.ReferenceDayAfterInduction),
            view.Timepoints,
            view.Parameters,
            view.Groups
                .Select(group => new StandardGroupDto(
                    group.Name,
                    group.Kind.ToString(),
                    group.DoseAmount,
                    group.DoseUnit))
                .ToList(),
            new DilutionDefaultsDto(
                view.DilutionDefaults.MicrolitresPerGram,
                view.DilutionDefaults.DefaultDiluent));
}
