using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.CollectionRoles;

/// <summary>
/// A per-tenant collection role (SISLAB-08): the configurable job a laboratory cadasters once and later assigns to a
/// person on a collection sheet — e.g. "Volante", "Anestesia", "Decapitação", "Sangue", "Medula", "Gânglio", "Nervo".
/// It is a pure declarative catalogue entry (a <see cref="Name"/> and an optional <see cref="Description"/>), the same
/// shape the module already uses for <c>Room</c>/<c>ItemCategory</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a Configuration catalogue, not a code enum.</b> The task is explicit that the list of collection roles is
/// "cadastro por laboratório", never a fixed enum in code. The current lab's seven roles become seven cadastered rows,
/// not constants — so a lab that collects a different tissue set simply cadasters its own roles. This is exactly the
/// role a per-tenant catalogue plays (Rooms, ItemCategories, Units), so the role lives here, in the Configuration
/// bounded context, and is consumed elsewhere <b>by value</b> (its id) through the public <c>ILabConfiguration</c> port.
/// </para>
/// <para>
/// <b>Identity.</b> A role is identified within a tenant by its <see cref="Name"/> (a unique index enforces one role
/// per name per company). The aggregate stays intentionally minimal — a name and a description — and grows only when a
/// real invariant arrives, mirroring the <c>Room</c> decision.
/// </para>
/// </remarks>
public sealed class CollectionRole : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 80;
    private const int MaxDescriptionLength = 500;

    // Parameterless constructor for EF Core materialization.
    private CollectionRole() : base(Guid.Empty) { }

    private CollectionRole(Guid id, string name, string? description) : base(id)
    {
        Name = name;
        Description = description;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Human-readable role name (unique per tenant), e.g. "Anestesia".</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Optional free-text description of what the role covers.</summary>
    public string? Description { get; private set; }

    /// <summary>Creates a new tenant collection role with a validated name and optional description.</summary>
    public static CollectionRole Create(string name, string? description = null)
        => new(Guid.NewGuid(), NormalizeName(name), NormalizeDescription(description));

    /// <summary>Renames the role (still unique per tenant), keeping its identity.</summary>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Sets or clears the role's description.</summary>
    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    private static string NormalizeName(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmed = name.Trim();
        Guard.AgainstMaxLength(trimmed, MaxNameLength, nameof(name));
        return trimmed;
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        string trimmed = description.Trim();
        Guard.AgainstMaxLength(trimmed, MaxDescriptionLength, nameof(description));
        return trimmed;
    }
}
