namespace SISLAB.Modules.Agenda.Contracts;

/// <summary>
/// Public inbound port of the Agenda module for programmatically creating calendar entries from another bounded
/// context (SISLAB-10). It is the single seam the Experiments module uses to materialise a generated experiment
/// schedule on the shared calendar; the caller never references the Agenda Domain, Application or Infrastructure
/// (module isolation, section 2), only this Contracts assembly.
/// </summary>
/// <remarks>
/// <para>
/// The implementation (in the module's Application) translates each <see cref="ScheduleAgendaEntryRequest"/> into
/// the module's own <c>CreateAgendaEntryCommand</c> and dispatches it through the mediator, so every entry is
/// created through the same validated write path — including the recurrence/reminder handling and the advisory
/// conflict check — as an entry created through the HTTP API.
/// </para>
/// <para>
/// <b>Tenancy.</b> The owning company is resolved by the Agenda write side from <c>ITenantContext</c>, never taken
/// from the request, matching the write-side rule everywhere else (defense-in-depth, section 7). A caller therefore
/// cannot schedule entries into another tenant's calendar through this surface.
/// </para>
/// </remarks>
public interface IAgendaScheduler
{
    /// <summary>
    /// Creates the supplied calendar entries for the active company, one per request, and returns the ids of the
    /// created entries in the same order. Each entry is created through the module's standard write path.
    /// </summary>
    /// <param name="requests">The entries to create, already positioned/assigned by the producing module.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Guid>> ScheduleAsync(
        IReadOnlyList<ScheduleAgendaEntryRequest> requests,
        CancellationToken cancellationToken = default);
}
