using SISLAB.Modules.Inventory.Domain.Partners.Events;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Domain.Partners;

/// <summary>
/// An external organization or person the laboratory deals with: a supplier of inputs/reagents/analyses,
/// a client/collaborator, or both (screen "Parceiros" #48). Partners are not system users; they are the
/// <b>origin</b> of stock entries and of samples/compounds sent for testing, and they feed traceability
/// and reports.
/// </summary>
/// <remarks>
/// <para>
/// Supply invariant: only a partner that supplies (<see cref="PartnerType.Supplier"/> or
/// <see cref="PartnerType.Both"/>) may be recorded as the origin of a stock entry. A stock item references
/// its supplier by value (a <see cref="Guid"/>), so the <see cref="StockItems.StockItem"/> aggregate
/// cannot enforce this on its own; the entry handler (card [E3] #24/#28) loads the partner and calls
/// <see cref="EnsureCanSupply"/> before applying the entry, so the rule lives with the aggregate that owns
/// the knowledge — the same pattern used by <see cref="StorageLocations.StorageLocation.EnsureCanStore"/>.
/// </para>
/// <para>
/// Samples (<see cref="SampleNote"/>) are a light, descriptive record of the compounds a partner sent for
/// testing. A first-class "test compound" entity belongs to the Experiments module (out of the current
/// backlog) and is intentionally not modelled here (decision recorded on card [E3] #28).
/// </para>
/// </remarks>
public sealed class Partner : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 200;
    private const int MaxDocumentLength = 40;
    private const int MaxDescriptionLength = 1000;
    private const int MaxSamples = 200;

    private readonly List<SampleNote> _samples = [];

    // Parameterless constructor for EF Core materialization.
    private Partner() : base(Guid.Empty) { }

    private Partner(
        Guid id,
        string name,
        PartnerType type,
        string? document,
        Email? contactEmail,
        string? description)
        : base(id)
    {
        Name = name;
        Type = type;
        Document = document;
        ContactEmail = contactEmail;
        Description = description;
        IsActive = true;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    public string Name { get; private set; } = default!;

    public PartnerType Type { get; private set; }

    /// <summary>Free-text registration document (e.g. CNPJ/registry code); optional.</summary>
    public string? Document { get; private set; }

    /// <summary>Institutional contact e-mail; optional.</summary>
    public Email? ContactEmail { get; private set; }

    /// <summary>What the partner supplies/does (e.g. "Reagentes, MTT, LPS, CFA"); optional.</summary>
    public string? Description { get; private set; }

    /// <summary>Whether the partner is in service. An inactive partner cannot be an entry origin.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Samples/compounds this partner sent for testing (lightweight, descriptive records).</summary>
    public IReadOnlyList<SampleNote> Samples => _samples.AsReadOnly();

    /// <summary>True when the partner's type allows it to be recorded as the origin of a stock entry.</summary>
    public bool IsSupplier => Type is PartnerType.Supplier or PartnerType.Both;

    /// <summary>
    /// Registers a new partner. The contact e-mail and document are optional; an invalid e-mail is
    /// rejected by the <see cref="Email"/> value object.
    /// </summary>
    public static Partner Register(
        string name,
        PartnerType type,
        string? document = null,
        string? contactEmail = null,
        string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        var partner = new Partner(
            Guid.NewGuid(),
            trimmedName,
            type,
            NormalizeOptionalText(document, MaxDocumentLength, nameof(document)),
            Email.FromValue(contactEmail),
            NormalizeOptionalText(description, MaxDescriptionLength, nameof(description)));

        partner.RaiseDomainEvent(new PartnerRegisteredEvent(partner.Id, partner.Name, partner.Type));
        return partner;
    }

    /// <summary>Renames the partner.</summary>
    public void Rename(string name)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Name = trimmedName;
    }

    /// <summary>Changes the role the partner plays for the laboratory.</summary>
    public void ChangeType(PartnerType type) => Type = type;

    /// <summary>Updates the registration document; passing null or blank clears it.</summary>
    public void UpdateDocument(string? document)
        => Document = NormalizeOptionalText(document, MaxDocumentLength, nameof(document));

    /// <summary>Updates the contact e-mail; passing null or blank clears it. Invalid addresses are rejected.</summary>
    public void UpdateContactEmail(string? contactEmail) => ContactEmail = Email.FromValue(contactEmail);

    /// <summary>Updates the free-text description; passing null or blank clears it.</summary>
    public void DescribeAs(string? description)
        => Description = NormalizeOptionalText(description, MaxDescriptionLength, nameof(description));

    /// <summary>
    /// Records a sample/compound the partner sent for testing. Duplicate references are rejected so the
    /// same sample is not listed twice.
    /// </summary>
    public void RecordSample(SampleNote sample)
    {
        Guard.AgainstNull(sample, nameof(sample));

        if (_samples.Count >= MaxSamples)
            throw new DomainException(
                $"Partner '{Name}' already has the maximum of {MaxSamples} recorded samples.");

        bool alreadyRecorded = _samples.Any(existing =>
            existing.Reference.Equals(sample.Reference, StringComparison.OrdinalIgnoreCase));
        if (alreadyRecorded)
            throw new DomainException(
                $"Sample '{sample.Reference}' is already recorded for partner '{Name}'.");

        _samples.Add(sample);
    }

    /// <summary>Removes a previously recorded sample by its reference. Fails if the reference is unknown.</summary>
    public void RemoveSample(string reference)
    {
        Guard.AgainstNullOrWhiteSpace(reference, nameof(reference));

        SampleNote? sample = _samples.FirstOrDefault(existing =>
            existing.Reference.Equals(reference.Trim(), StringComparison.OrdinalIgnoreCase));
        if (sample is null)
            throw new DomainException(
                $"Sample '{reference}' is not recorded for partner '{Name}'.");

        _samples.Remove(sample);
    }

    /// <summary>Takes the partner out of service. Idempotent.</summary>
    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        RaiseDomainEvent(new PartnerDeactivatedEvent(Id));
    }

    /// <summary>Puts a deactivated partner back in service. Idempotent.</summary>
    public void Reactivate()
    {
        if (IsActive)
            return;

        IsActive = true;
        RaiseDomainEvent(new PartnerReactivatedEvent(Id));
    }

    /// <summary>
    /// True when this partner may be recorded as the origin of a stock entry: it must both supply and be
    /// active. Does not throw; use <see cref="EnsureCanSupply"/> for the guard that reports why not.
    /// </summary>
    public bool CanSupply() => IsSupplier && IsActive;

    /// <summary>
    /// Guards that this partner may be the origin of a stock entry: it must be a supplier
    /// (<see cref="PartnerType.Supplier"/> or <see cref="PartnerType.Both"/>) and active. Called by the
    /// stock-entry handler on the informed supplier before the entry is applied (card [E3] #28).
    /// </summary>
    public void EnsureCanSupply()
    {
        if (!IsSupplier)
            throw new BusinessException(
                $"Partner '{Name}' is a {Type} and cannot be the supplier of a stock entry.");

        if (!IsActive)
            throw new BusinessException(
                $"Partner '{Name}' is inactive and cannot be the supplier of a stock entry.");
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
