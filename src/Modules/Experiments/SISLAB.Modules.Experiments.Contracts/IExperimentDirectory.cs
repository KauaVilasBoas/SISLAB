namespace SISLAB.Modules.Experiments.Contracts;

/// <summary>
/// Read-only public boundary of the Experiments module for resolving an experiment's display name by id
/// (card [E10.4] #4). It is the <b>only</b> surface other modules may use to look up an experiment: the Agenda
/// module carries an <c>ExperimentId</c> by value on its calendar entries and needs the human-readable title
/// for the calendar projection, but must never JOIN the <c>experiments</c> schema or reference the Experiments
/// Domain/Application/Infrastructure (module isolation, section 2; enforced by the architecture tests).
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant scoping.</b> Every lookup is implicitly scoped to the active company; the <c>CompanyId</c> is
/// resolved by the adapter from <c>ITenantContext</c>, never passed by the caller — a consuming module cannot
/// read another tenant's experiments through this surface (defense-in-depth, section 7).
/// </para>
/// <para>
/// <b>Batch by design.</b> The calendar resolves many entries at once, so the primary operation takes a set of
/// ids and returns a map — one round-trip for a whole calendar page instead of N.
/// </para>
/// </remarks>
public interface IExperimentDirectory
{
    /// <summary>
    /// Returns a map of experiment id → title for those <paramref name="experimentIds"/> that exist for the
    /// active company. Ids that do not exist (or belong to another company) are simply absent from the map, so
    /// the caller falls back gracefully to showing no name.
    /// </summary>
    /// <param name="experimentIds">The distinct experiment ids to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyDictionary<Guid, string>> GetTitlesAsync(
        IReadOnlyCollection<Guid> experimentIds,
        CancellationToken ct);
}
