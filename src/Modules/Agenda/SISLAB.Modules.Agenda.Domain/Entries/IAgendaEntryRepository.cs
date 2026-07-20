namespace SISLAB.Modules.Agenda.Domain.Entries;

/// <summary>
/// Aggregate repository for <see cref="AgendaEntry"/> (card [E10.1] #1). Interface lives in the Domain, its
/// EF implementation in the Infrastructure (Dependency Inversion). Only the write-side loads whole aggregates
/// through it; the calendar read models use Dapper directly.
/// </summary>
public interface IAgendaEntryRepository
{
    /// <summary>Loads the entry with <paramref name="id"/> of the active company, or <see langword="null"/>.</summary>
    Task<AgendaEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stages a new entry for insertion on the next unit-of-work commit.</summary>
    void Add(AgendaEntry entry);

    /// <summary>Stages an entry for removal on the next unit-of-work commit (full-series delete).</summary>
    void Remove(AgendaEntry entry);
}
