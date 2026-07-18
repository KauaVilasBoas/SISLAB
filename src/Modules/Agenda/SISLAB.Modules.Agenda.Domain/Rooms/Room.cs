using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Domain.Rooms;

public sealed class Room : Entity<Guid>, ITenantEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = default!;
    public int Capacity { get; private set; }
    public RoomType Type { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Room() : base(Guid.Empty) { }

    private Room(Guid id, Guid companyId, string name, int capacity, RoomType type, DateTime createdAtUtc)
        : base(id)
    {
        CompanyId = companyId;
        Name = name;
        Capacity = capacity;
        Type = type;
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
    }

    public static Room Register(Guid companyId, string name, int capacity, RoomType type, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));

        return new Room(Guid.NewGuid(), companyId, name.Trim(), capacity, type, createdAtUtc);
    }

    public void Update(string name, int capacity, RoomType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Name = name.Trim();
        Capacity = capacity;
        Type = type;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
