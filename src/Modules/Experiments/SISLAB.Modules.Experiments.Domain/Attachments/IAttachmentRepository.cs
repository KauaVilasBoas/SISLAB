namespace SISLAB.Modules.Experiments.Domain.Attachments;

/// <summary>
/// Repository for the <see cref="Attachment"/> aggregate (interface in Domain, EF implementation in Infrastructure).
/// Reads are implicitly tenant-scoped by the write-side global query filter; the commit is owned by the unit of work
/// (<c>TransactionBehavior</c> → <c>IUnitOfWork.SaveChangesAsync</c>), so the repository never saves.
/// </summary>
public interface IAttachmentRepository
{
    /// <summary>Loads an attachment by id, or null when it does not exist for the tenant.</summary>
    Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new attachment to the write set.</summary>
    Task AddAsync(Attachment attachment, CancellationToken ct = default);
}
