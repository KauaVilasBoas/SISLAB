using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Agenda.Domain.Bioterium;
using SISLAB.Modules.Agenda.Infrastructure.Persistence;

namespace SISLAB.Modules.Agenda.Infrastructure.Repositories;

internal sealed class BioteriumRepository : IBioteriumRepository
{
    private readonly AgendaDbContext _db;

    public BioteriumRepository(AgendaDbContext db) => _db = db;

    public Task<BioteriumAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.BioteriumAssignments.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BioteriumAssignment>> GetByDateRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
        => await _db.BioteriumAssignments
            .Where(a => a.AssignmentDate >= from && a.AssignmentDate <= to)
            .OrderBy(a => a.AssignmentDate)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsForDateAsync(DateOnly date, CancellationToken cancellationToken = default)
        => _db.BioteriumAssignments.AnyAsync(a => a.AssignmentDate == date, cancellationToken);

    public void Add(BioteriumAssignment assignment) => _db.BioteriumAssignments.Add(assignment);
}
