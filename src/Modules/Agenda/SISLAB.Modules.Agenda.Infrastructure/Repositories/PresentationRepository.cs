using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.Modules.Agenda.Infrastructure.Persistence;

namespace SISLAB.Modules.Agenda.Infrastructure.Repositories;

internal sealed class PresentationRepository : IPresentationRepository
{
    private readonly AgendaDbContext _db;

    public PresentationRepository(AgendaDbContext db) => _db = db;

    public Task<Presentation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.Presentations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Presentation>> GetByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => await _db.Presentations
            .Where(p => p.ScheduledDate >= from && p.ScheduledDate <= to)
            .OrderBy(p => p.ScheduledDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Presentation>> GetUpcomingWithoutReminderAsync(
        DateOnly today,
        int withinDays,
        CancellationToken cancellationToken = default)
    {
        DateOnly threshold = today.AddDays(withinDays);
        return await _db.Presentations
            .Where(p => p.Status == PresentationStatus.Scheduled
                     && p.ReminderSentAt == null
                     && p.ScheduledDate >= today
                     && p.ScheduledDate <= threshold)
            .OrderBy(p => p.ScheduledDate)
            .ToListAsync(cancellationToken);
    }

    public void Add(Presentation presentation) => _db.Presentations.Add(presentation);
}
