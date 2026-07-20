using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Agenda.Domain.Entries;
using SISLAB.Modules.Agenda.Infrastructure.Persistence;

namespace SISLAB.Modules.Agenda.Infrastructure.Repositories;

internal sealed class AgendaEntryRepository : IAgendaEntryRepository
{
    private readonly AgendaDbContext _db;

    public AgendaEntryRepository(AgendaDbContext db) => _db = db;

    public Task<AgendaEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.AgendaEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(AgendaEntry entry) => _db.AgendaEntries.Add(entry);

    public void Remove(AgendaEntry entry) => _db.AgendaEntries.Remove(entry);
}
