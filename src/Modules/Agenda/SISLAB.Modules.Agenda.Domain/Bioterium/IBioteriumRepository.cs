namespace SISLAB.Modules.Agenda.Domain.Bioterium;

public interface IBioteriumRepository
{
    Task<BioteriumAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns assignments in the given date range, ordered by date ascending.</summary>
    Task<IReadOnlyList<BioteriumAssignment>> GetByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any assignment already exists for the given date
    /// (prevents double-generating the schedule for the same week).
    /// </summary>
    Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken cancellationToken = default);

    void Add(BioteriumAssignment assignment);
}
