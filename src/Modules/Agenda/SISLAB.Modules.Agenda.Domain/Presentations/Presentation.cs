using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Domain.Presentations;

/// <summary>
/// A scheduled presentation (card [E10] #71): LAFTE seminars or DOL journal-club entries planned by
/// semester. A 15-day advance reminder is sent automatically (flagged by <see cref="ReminderSentAt"/>).
/// </summary>
public sealed class Presentation : AggregateRoot<Guid>, ITenantEntity
{
    public Guid CompanyId { get; private set; }
    public PresentationType Type { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Doi { get; private set; }
    public string PresenterName { get; private set; } = default!;
    public DateOnly ScheduledDate { get; private set; }
    public DateTime? ReminderSentAt { get; private set; }
    public PresentationStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Presentation() : base(Guid.Empty) { }

    private Presentation(
        Guid id, Guid companyId, PresentationType type, string title, string? doi,
        string presenterName, DateOnly scheduledDate, string? notes, DateTime createdAtUtc)
        : base(id)
    {
        CompanyId = companyId;
        Type = type;
        Title = title;
        Doi = doi;
        PresenterName = presenterName;
        ScheduledDate = scheduledDate;
        Notes = notes;
        Status = PresentationStatus.Scheduled;
        CreatedAtUtc = createdAtUtc;
    }

    public static Presentation Schedule(
        Guid companyId, PresentationType type, string title, string? doi,
        string presenterName, DateOnly scheduledDate, string? notes, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(presenterName);

        return new Presentation(
            Guid.NewGuid(), companyId, type,
            title.Trim(), doi?.Trim(), presenterName.Trim(),
            scheduledDate, notes?.Trim(), createdAtUtc);
    }

    public void Reschedule(DateOnly newDate, string? notes = null)
    {
        ScheduledDate = newDate;
        if (notes is not null) Notes = notes.Trim();

        // Clear the reminder flag so the (recomputed) advance reminder fires again for the new
        // date — the reminder query filters on ReminderSentAt IS NULL.
        ReminderSentAt = null;
    }

    public void Cancel() => Status = PresentationStatus.Cancelled;
    public void MarkDone() => Status = PresentationStatus.Done;
    public void RecordReminderSent(DateTime sentAt) => ReminderSentAt = sentAt;
}
