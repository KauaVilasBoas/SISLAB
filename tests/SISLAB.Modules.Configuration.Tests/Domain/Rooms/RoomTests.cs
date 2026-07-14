using SISLAB.Modules.Configuration.Domain.Rooms;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Tests.Domain.Rooms;

/// <summary>
/// Covers the per-tenant <see cref="Room"/> aggregate (card [E12] #76): name normalization and the
/// "requires authorization" flag the future Agenda module (card [E10]) will use for booking sign-off.
/// </summary>
public sealed class RoomTests
{
    [Fact]
    public void Create_trims_the_name_and_defaults_to_no_authorization()
    {
        Room room = Room.Create("  Sala de Cultivo  ");

        Assert.Equal("Sala de Cultivo", room.Name);
        Assert.False(room.RequiresAuthorization);
    }

    [Fact]
    public void Room_is_tenant_scoped()
    {
        Assert.IsAssignableFrom<ITenantEntity>(Room.Create("Sala"));
    }

    [Fact]
    public void Create_keeps_the_authorization_flag()
    {
        Room room = Room.Create("Biotério", requiresAuthorization: true);

        Assert.True(room.RequiresAuthorization);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_name(string? blank)
    {
        Assert.Throws<DomainException>(() => Room.Create(blank!));
    }

    [Fact]
    public void Rename_changes_the_name_keeping_identity()
    {
        Room room = Room.Create("Sala 1");
        Guid id = room.Id;

        room.Rename("  Sala 2  ");

        Assert.Equal("Sala 2", room.Name);
        Assert.Equal(id, room.Id);
    }

    [Fact]
    public void SetRequiresAuthorization_toggles_the_flag()
    {
        Room room = Room.Create("Sala");

        room.SetRequiresAuthorization(true);
        Assert.True(room.RequiresAuthorization);

        room.SetRequiresAuthorization(false);
        Assert.False(room.RequiresAuthorization);
    }
}
