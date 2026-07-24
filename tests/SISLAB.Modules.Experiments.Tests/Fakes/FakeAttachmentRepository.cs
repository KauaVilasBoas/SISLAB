using SISLAB.Modules.Experiments.Domain.Attachments;

namespace SISLAB.Modules.Experiments.Tests.Fakes;

/// <summary>
/// In-memory fake of <see cref="IAttachmentRepository"/> for handler unit tests — no EF, no database. Records the last
/// added aggregate so tests can assert on the persisted instance.
/// </summary>
internal sealed class FakeAttachmentRepository : IAttachmentRepository
{
    private readonly Dictionary<Guid, Attachment> _store = new();

    public Attachment? LastAdded { get; private set; }

    public Task<Attachment?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_store.GetValueOrDefault(id));

    public Task AddAsync(Attachment attachment, CancellationToken ct = default)
    {
        _store[attachment.Id] = attachment;
        LastAdded = attachment;
        return Task.CompletedTask;
    }
}
