using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// A per-tenant experimental model / induction protocol (SISLAB-04): the reusable template a laboratory cadasters
/// once (e.g. "Neuropatia diabética", "Ligadura de nervo ciático") and later binds batches/experiments to
/// (sub-task 2). It captures, declaratively, everything that today is implicit convention on the spreadsheet: the
/// induction protocol, the standard timepoints, which physiological parameters apply, the default group design and
/// the default dilution parameters that seed the in vivo preparation (SISLAB-01).
/// </summary>
/// <remarks>
/// <para>
/// <b>The generalization core.</b> The current laboratory's two models (ND and sciatic-nerve ligation) become two
/// cadastered rows, not code. Naive/Controle/3 g·kg⁻¹/0,6 g·kg⁻¹, the 1 g : 5 µL relation, soy-oil vehicle, the
/// glicemia/rotarod/peso parameters and the basal→28°-dia timepoints are all example data carried by value
/// objects, never constants.
/// </para>
/// <para>
/// <b>Identity.</b> A model is identified within a tenant by its <see cref="Name"/> (a unique index enforces it).
/// Every compositional invariant lives in a value object (<see cref="InductionProtocol"/>,
/// <see cref="StandardTimepoints"/>, <see cref="ApplicableParameters"/>, <see cref="StandardGroups"/>,
/// <see cref="DilutionDefaults"/>), so the aggregate stays a thin, rich orchestrator that can never hold a
/// partially-valid model.
/// </para>
/// </remarks>
public sealed class ExperimentalModel : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 120;
    private const int MaxDescriptionLength = 500;

    // Parameterless constructor for EF Core materialization.
    private ExperimentalModel() : base(Guid.Empty) { }

    private ExperimentalModel(
        Guid id,
        string name,
        string? description,
        InductionProtocol induction,
        StandardTimepoints timepoints,
        ApplicableParameters parameters,
        StandardGroups groups,
        DilutionDefaults dilutionDefaults) : base(id)
    {
        Name = name;
        Description = description;
        Induction = induction;
        Timepoints = timepoints;
        Parameters = parameters;
        Groups = groups;
        DilutionDefaults = dilutionDefaults;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>The model's name (unique per tenant), e.g. "Neuropatia diabética".</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Optional free-text description of the model.</summary>
    public string? Description { get; private set; }

    /// <summary>The induction protocol (administrations, spacing, reference day).</summary>
    public InductionProtocol Induction { get; private set; } = default!;

    /// <summary>The default timepoints the model measures at.</summary>
    public StandardTimepoints Timepoints { get; private set; } = default!;

    /// <summary>The physiological/behavioural parameters that apply to this model.</summary>
    public ApplicableParameters Parameters { get; private set; } = default!;

    /// <summary>The default group design (Naive/Control/dose curve).</summary>
    public StandardGroups Groups { get; private set; } = default!;

    /// <summary>The default dilution parameters (g:µL relation + default diluent) seeding the in vivo preparation.</summary>
    public DilutionDefaults DilutionDefaults { get; private set; } = default!;

    /// <summary>Creates a fully-formed experimental model for the active company from validated value objects.</summary>
    public static ExperimentalModel Create(
        string name,
        string? description,
        InductionProtocol induction,
        StandardTimepoints timepoints,
        ApplicableParameters parameters,
        StandardGroups groups,
        DilutionDefaults dilutionDefaults)
    {
        Guard.AgainstNull(induction, nameof(induction));
        Guard.AgainstNull(timepoints, nameof(timepoints));
        Guard.AgainstNull(parameters, nameof(parameters));
        Guard.AgainstNull(groups, nameof(groups));
        Guard.AgainstNull(dilutionDefaults, nameof(dilutionDefaults));

        return new ExperimentalModel(
            Guid.NewGuid(),
            NormalizeName(name),
            NormalizeDescription(description),
            induction,
            timepoints,
            parameters,
            groups,
            dilutionDefaults);
    }

    /// <summary>Renames the model (still unique per tenant), keeping its identity.</summary>
    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Sets or clears the model's description.</summary>
    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    /// <summary>Replaces the induction protocol.</summary>
    public void ChangeInduction(InductionProtocol induction)
        => Induction = Guard.AgainstNull(induction, nameof(induction));

    /// <summary>Replaces the standard timepoints.</summary>
    public void ChangeTimepoints(StandardTimepoints timepoints)
        => Timepoints = Guard.AgainstNull(timepoints, nameof(timepoints));

    /// <summary>Replaces the applicable parameter set.</summary>
    public void ChangeParameters(ApplicableParameters parameters)
        => Parameters = Guard.AgainstNull(parameters, nameof(parameters));

    /// <summary>Replaces the default group design.</summary>
    public void ChangeGroups(StandardGroups groups)
        => Groups = Guard.AgainstNull(groups, nameof(groups));

    /// <summary>Replaces the default dilution parameters.</summary>
    public void ChangeDilutionDefaults(DilutionDefaults dilutionDefaults)
        => DilutionDefaults = Guard.AgainstNull(dilutionDefaults, nameof(dilutionDefaults));

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
