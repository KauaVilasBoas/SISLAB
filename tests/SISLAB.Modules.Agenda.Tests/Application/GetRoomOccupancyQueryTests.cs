using SISLAB.Modules.Agenda.Application.Rooms.Queries;

namespace SISLAB.Modules.Agenda.Tests.Application;

/// <summary>
/// Tests for the room-lane projection of the occupancy read model (card [E10.11]). The SQL/expansion path needs
/// a live Postgres and is exercised elsewhere; here we cover the pure room-name resolution the Gantt groups by,
/// which is what makes <see cref="RoomOccupancySlot.RoomId"/> / <see cref="RoomOccupancySlot.RoomName"/> non-null.
/// </summary>
public sealed class GetRoomOccupancyQueryTests
{
    [Fact]
    public void ResolveRoomName_PrefersJoinedRoomName()
    {
        var roomId = Guid.NewGuid();

        string? name = GetRoomOccupancyQueryHandler.ResolveRoomName(roomId, "Sala 1");

        Assert.Equal("Sala 1", name);
    }

    [Fact]
    public void ResolveRoomName_FallsBackToRoomIdWhenRoomRowMissing()
    {
        var roomId = Guid.NewGuid();

        string? name = GetRoomOccupancyQueryHandler.ResolveRoomName(roomId, joinedName: null);

        Assert.Equal(roomId.ToString(), name);
    }

    [Fact]
    public void ResolveRoomName_IsNullWhenEntryHasNoRoom()
        => Assert.Null(GetRoomOccupancyQueryHandler.ResolveRoomName(roomId: null, joinedName: null));
}
