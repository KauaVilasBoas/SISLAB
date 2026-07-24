using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A single animal enrolled in an in vivo study (card [E11] #73, SISLAB-03). It is the subject an
/// application/measurement is recorded against at each timepoint. Identified for the operator by a within-project
/// unique <see cref="Identifier"/> (an ear tag / cage code, e.g. "CX7-A4"), it carries its <see cref="Sex"/> and an
/// optional baseline <see cref="WeightGrams"/>.
/// </summary>
/// <remarks>
/// <para>
/// An animal is a child entity of the <see cref="Project"/> aggregate, physically <b>housed in a <see cref="Cage"/></b>
/// (SISLAB-03), and only ever mutated through the aggregate. Its treatment <see cref="Group"/> is an <b>optional
/// assignment</b> held by value (<see cref="GroupId"/>): an animal may exist in a cage with no group yet (measured
/// basal before randomization) and be assigned/moved to a group later. This is what lets the same model serve both
/// randomization moments — before or after induction.
/// </para>
/// <para>
/// The surrogate <see cref="Entity{TId}.Id"/> is the table key; the <see cref="Identifier"/> is the natural label the
/// aggregate keeps unique within a project so a timepoint launch can name the animal unambiguously.
/// </para>
/// </remarks>
public sealed class Animal : Entity<Guid>
{
    private const int MaxIdentifierLength = 60;

    // Parameterless constructor for EF Core materialization.
    private Animal() : base(Guid.Empty) => Identifier = default!;

    private Animal(Guid id, string identifier, AnimalSex sex, decimal? weightGrams, Guid? groupId)
        : base(id)
    {
        Identifier = identifier;
        Sex = sex;
        WeightGrams = weightGrams;
        GroupId = groupId;
    }

    /// <summary>Operator-facing identifier (ear tag / cage code), unique within the project.</summary>
    public string Identifier { get; private set; }

    /// <summary>Biological sex of the animal.</summary>
    public AnimalSex Sex { get; private set; }

    /// <summary>Optional baseline body weight in grams; null when not recorded.</summary>
    public decimal? WeightGrams { get; private set; }

    /// <summary>
    /// The treatment <see cref="Group"/> (dose) this animal is assigned to, held <b>by value</b> (SISLAB-03), or
    /// <see langword="null"/> while the animal is still unassigned (in a cage, pre-randomization). The referenced group
    /// is owned by the same <see cref="Batch"/>; the aggregate validates it exists before assigning — no navigation.
    /// </summary>
    public Guid? GroupId { get; private set; }

    /// <summary>
    /// The most recent inclusion decision taken on this animal (SISLAB-02), or <see langword="null"/> while no
    /// selection criterion has been applied. Only ever set through the owning <see cref="Project"/> aggregate.
    /// </summary>
    public InclusionDecision? Inclusion { get; private set; }

    /// <summary>Creates an animal, guarding a present identifier and a non-negative weight when supplied.</summary>
    internal static Animal Create(string identifier, AnimalSex sex, decimal? weightGrams = null, Guid? groupId = null)
    {
        Guard.AgainstNullOrWhiteSpace(identifier, nameof(identifier));
        string trimmedIdentifier = identifier.Trim();
        Guard.AgainstMaxLength(trimmedIdentifier, MaxIdentifierLength, nameof(identifier));

        if (weightGrams is { } weight && weight < 0)
            throw new DomainException($"Animal weight cannot be negative. Received: {weight}.");

        return new Animal(Guid.NewGuid(), trimmedIdentifier, sex, weightGrams, groupId);
    }

    /// <summary>
    /// Assigns (or moves) this animal to a treatment group (SISLAB-03), replacing any prior assignment. Only ever
    /// called through the owning <see cref="Project"/> aggregate, which has already validated the group belongs to the
    /// animal's batch. A re-assignment to a different group is how a discrepant cage is redistributed.
    /// </summary>
    internal void AssignToGroup(Guid groupId)
        => GroupId = Guard.AgainstEmptyGuid(groupId, nameof(groupId));

    /// <summary>Removes this animal's group assignment (back to unassigned). Only called through the aggregate.</summary>
    internal void Unassign() => GroupId = null;

    /// <summary>
    /// Records the outcome of applying an inclusion criterion to this animal (SISLAB-02), replacing any previous
    /// decision. Only ever called through the owning <see cref="Project"/> aggregate, which owns the evaluation.
    /// </summary>
    internal void RecordInclusion(InclusionDecision decision)
        => Inclusion = Guard.AgainstNull(decision, nameof(decision));
}
