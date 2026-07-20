using SISLAB.Modules.Agenda.Domain.Entries;

namespace SISLAB.Modules.Agenda.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAgendaEntryRepository"/> for command-handler tests: records added/removed aggregates
/// and serves seeded ones by id, so a handler's orchestration can be asserted without a database.
/// </summary>
internal sealed class FakeAgendaEntryRepository : IAgendaEntryRepository
{
    private readonly Dictionary<Guid, AgendaEntry> _store = new();

    public List<AgendaEntry> Added { get; } = [];
    public List<AgendaEntry> Removed { get; } = [];

    public AgendaEntry? LastAdded => Added.Count > 0 ? Added[^1] : null;

    public FakeAgendaEntryRepository Seed(AgendaEntry entry)
    {
        _store[entry.Id] = entry;
        return this;
    }

    public Task<AgendaEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.GetValueOrDefault(id));

    public void Add(AgendaEntry entry)
    {
        _store[entry.Id] = entry;
        Added.Add(entry);
    }

    public void Remove(AgendaEntry entry)
    {
        _store.Remove(entry.Id);
        Removed.Add(entry);
    }
}
