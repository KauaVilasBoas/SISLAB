using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Experiments.Domain.Attachments.Events;

/// <summary>
/// Raised when a piece of evidence is attached to an animal's reading/analysis (SISLAB-09). Module-internal for now (no
/// Outbox translator); carried so a future read-model projection or audit trail can react. <see cref="CompanyId"/>
/// travels on the event for a potential Outbox translation later.
/// </summary>
public sealed record AttachmentRegisteredEvent(
    Guid CompanyId,
    Guid AttachmentId,
    Guid AnimalId,
    AttachmentTargetKind TargetKind,
    Guid TargetId) : IDomainEvent
{
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
