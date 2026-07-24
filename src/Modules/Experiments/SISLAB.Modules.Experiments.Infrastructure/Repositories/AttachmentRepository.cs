using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.Modules.Experiments.Infrastructure.Persistence;

namespace SISLAB.Modules.Experiments.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAttachmentRepository"/> (SISLAB-09). Reads are implicitly tenant-scoped by the
/// write-side global query filter; the commit is owned by the unit of work (<c>TransactionBehavior</c> →
/// <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
internal sealed class AttachmentRepository : IAttachmentRepository
{
    private readonly ExperimentsDbContext _dbContext;

    public AttachmentRepository(ExperimentsDbContext dbContext) => _dbContext = dbContext;

    public async Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await _dbContext.Attachments.FirstOrDefaultAsync(attachment => attachment.Id == id, ct);

    public async Task AddAsync(Attachment attachment, CancellationToken ct = default)
        => await _dbContext.Attachments.AddAsync(attachment, ct);
}
