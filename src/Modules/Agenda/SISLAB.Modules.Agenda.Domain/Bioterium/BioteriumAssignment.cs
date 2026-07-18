using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Domain.Bioterium;

/// <summary>
/// A single biotério cage-cleaning assignment (card [E10] #70): one Monday or Thursday slot owned by
/// one member. Generated in weekly batches by the rotation algorithm; operators can swap slots between
/// members, preserving the swap history. Marking done closes the day's obligation.
/// </summary>
public sealed class BioteriumAssignment : AggregateRoot<Guid>, ITenantEntity
{
    public Guid CompanyId { get; private set; }
    public DateOnly AssignmentDate { get; private set; }
    public string ResponsibleName { get; private set; } = default!;
    public AssignmentStatus Status { get; private set; }
    public string? SwappedFromName { get; private set; }
    public string? SwapReason { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private BioteriumAssignment() : base(Guid.Empty) { }

    private BioteriumAssignment(Guid id, Guid companyId, DateOnly assignmentDate, string responsibleName, DateTime createdAtUtc)
        : base(id)
    {
        CompanyId = companyId;
        AssignmentDate = assignmentDate;
        ResponsibleName = responsibleName;
        Status = AssignmentStatus.Pending;
        CreatedAtUtc = createdAtUtc;
    }

    public static BioteriumAssignment Create(Guid companyId, DateOnly assignmentDate, string responsibleName, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsibleName);

        if (assignmentDate.DayOfWeek is not (DayOfWeek.Monday or DayOfWeek.Thursday))
            throw new ArgumentException("Biotério assignments must be on Mondays or Thursdays.", nameof(assignmentDate));

        return new BioteriumAssignment(Guid.NewGuid(), companyId, assignmentDate, responsibleName.Trim(), createdAtUtc);
    }

    public void Swap(string newResponsibleName, string? reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newResponsibleName);

        // A completed cleaning cannot be reassigned — swapping it would corrupt the done history.
        if (Status == AssignmentStatus.Done)
            throw new BusinessException("Não é possível permutar um serviço já realizado.");

        SwappedFromName = ResponsibleName;
        ResponsibleName = newResponsibleName.Trim();
        SwapReason = reason?.Trim();
        Status = AssignmentStatus.Swapped;
    }

    public void MarkDone(string? notes = null)
    {
        Notes = notes?.Trim();
        Status = AssignmentStatus.Done;
    }
}
