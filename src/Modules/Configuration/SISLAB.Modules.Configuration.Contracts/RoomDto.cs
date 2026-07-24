namespace SISLAB.Modules.Configuration.Contracts;

/// <summary>
/// Public, flattened view of a tenant's room (card [E12] #76), returned across the module boundary by
/// <see cref="ILabConfiguration"/>. It carries only primitives — never the internal <c>Room</c> aggregate — so a
/// consuming module (Experiments, for a collection plan's storage location) depends on nothing of the Configuration
/// Domain (module isolation, section 2), referencing a room only by its <see cref="Id"/>.
/// </summary>
/// <param name="Id">Stable identifier of the room, referenced by value by a sample routing's storage.</param>
/// <param name="Name">Human-readable room name (e.g. "Freezer −80 °C").</param>
/// <param name="RequiresAuthorization">Whether using/booking the room requires explicit authorization.</param>
public sealed record RoomDto(Guid Id, string Name, bool RequiresAuthorization);
