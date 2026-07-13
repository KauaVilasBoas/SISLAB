using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.Rooms;

/// <summary>
/// A per-tenant physical room of the laboratory (card [E12] #76), with the "requires authorization" flag
/// the future Agenda module (card [E10]) will use to decide whether booking the room needs sign-off.
/// </summary>
/// <remarks>
/// Configuration owns the declarative room catalogue only; there is no consumer yet (the Agenda is a later
/// card), so the aggregate deliberately stays minimal — a name and the authorization flag — and grows when
/// the Agenda's real invariants arrive, rather than speculating on them now.
/// </remarks>
public sealed class Room : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 120;

    // Parameterless constructor for EF Core materialization.
    private Room() : base(Guid.Empty) { }

    private Room(Guid id, string name, bool requiresAuthorization) : base(id)
    {
        Name = name;
        RequiresAuthorization = requiresAuthorization;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Human-readable room name (unique per tenant).</summary>
    public string Name { get; private set; } = default!;

    /// <summary>When true, using/booking this room requires explicit authorization (Agenda, card [E10]).</summary>
    public bool RequiresAuthorization { get; private set; }

    /// <summary>Creates a new tenant room with a validated name and its authorization flag.</summary>
    public static Room Create(string name, bool requiresAuthorization = false)
        => new(Guid.NewGuid(), NormalizeName(name), requiresAuthorization);

    /// <summary>Renames the room, keeping its identity.</summary>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Sets whether the room requires authorization to be used/booked.</summary>
    public void SetRequiresAuthorization(bool requiresAuthorization)
        => RequiresAuthorization = requiresAuthorization;

    private static string NormalizeName(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));
        return trimmed;
    }
}
