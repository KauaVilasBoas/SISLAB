namespace SISLAB.Modules.Agenda.Domain.Presentations;

public interface IPresentationRepository
{
    Task<Presentation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns presentations in the given date range, ordered by scheduled date ascending.</summary>
    Task<IReadOnlyList<Presentation>> GetByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns upcoming scheduled presentations whose reminder has not yet been sent and whose
    /// scheduled date is within <paramref name="withinDays"/> days of today (for the reminder job).
    /// </summary>
    Task<IReadOnlyList<Presentation>> GetUpcomingWithoutReminderAsync(
        DateOnly today,
        int withinDays,
        CancellationToken cancellationToken = default);

    void Add(Presentation presentation);
}
