namespace SISLAB.Modules.Configuration.Contracts;

/// <summary>
/// Public, flattened view of a tenant's collection role (SISLAB-08), returned across the module boundary by
/// <see cref="ILabConfiguration"/>. It carries only primitives — never the internal <c>CollectionRole</c> aggregate —
/// so the consuming module (Experiments) depends on nothing of the Configuration Domain (module isolation, section 2).
/// The Experiments collection plan references a role only by its <see cref="Id"/> (by value), validating it through
/// this surface.
/// </summary>
/// <param name="Id">Stable identifier of the role, referenced by value by the collection plan's assignments.</param>
/// <param name="Name">Human-readable role name (e.g. "Anestesia").</param>
/// <param name="Description">Optional description of what the role covers.</param>
public sealed record CollectionRoleDto(Guid Id, string Name, string? Description);
