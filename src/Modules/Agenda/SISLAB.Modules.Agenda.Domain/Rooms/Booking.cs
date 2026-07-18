using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Domain.Rooms;

/// <summary>
/// A room booking (card [E10] #69). Detects overlaps with other active bookings in the same room and
/// flags <see cref="HasConflictWarning"/> — the system alerts but does NOT block the reservation
/// (project decision: shared-room lab reality means hard blocks create friction without safety gain).
/// </summary>
public sealed class Booking : AggregateRoot<Guid>, ITenantEntity
{
    public Guid CompanyId { get; private set; }
    public Guid RoomId { get; private set; }
    public string BookedByName { get; private set; } = default!;
    public AgendaActivity Activity { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public string? Notes { get; private set; }
    public BookingStatus Status { get; private set; }
    public bool HasConflictWarning { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Booking() : base(Guid.Empty) { }

    private Booking(
        Guid id, Guid companyId, Guid roomId, string bookedByName,
        AgendaActivity activity, DateOnly date, TimeOnly startTime, TimeOnly endTime,
        string? notes, bool overlapsExist, DateTime createdAtUtc) : base(id)
    {
        CompanyId = companyId;
        RoomId = roomId;
        BookedByName = bookedByName;
        Activity = activity;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
        Notes = notes;
        Status = BookingStatus.Active;
        HasConflictWarning = overlapsExist;
        CreatedAtUtc = createdAtUtc;
    }

    public static Booking Create(
        Guid companyId, Guid roomId, string bookedByName,
        AgendaActivity activity, DateOnly date, TimeOnly startTime, TimeOnly endTime,
        string? notes, bool overlapsExist, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookedByName);
        if (endTime <= startTime)
            throw new ArgumentException("End time must be after start time.", nameof(endTime));

        return new Booking(
            Guid.NewGuid(), companyId, roomId, bookedByName.Trim(),
            activity, date, startTime, endTime, notes?.Trim(), overlapsExist, createdAtUtc);
    }

    public void Cancel() => Status = BookingStatus.Cancelled;

    public bool OverlapsWith(DateOnly date, TimeOnly startTime, TimeOnly endTime)
        => Status == BookingStatus.Active
           && Date == date
           && StartTime < endTime
           && EndTime > startTime;
}
