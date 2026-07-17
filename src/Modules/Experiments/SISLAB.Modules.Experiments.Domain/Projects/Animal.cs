using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A single animal enrolled in an in vivo study (card [E11] #73). It is the subject an application/measurement is
/// recorded against at each timepoint. Identified for the operator by a within-project unique
/// <see cref="Identifier"/> (an ear tag / cage code, e.g. "M1-07"), it carries its <see cref="Sex"/> and an
/// optional baseline <see cref="WeightGrams"/>.
/// </summary>
/// <remarks>
/// An animal is a child entity of the <see cref="Project"/> aggregate, reached through its <see cref="Group"/> and
/// <see cref="Batch"/>, and only ever mutated through the aggregate. The surrogate <see cref="Entity{TId}.Id"/> is
/// the table key; the <see cref="Identifier"/> is the natural label the aggregate keeps unique within a project so
/// a timepoint launch can name the animal unambiguously.
/// </remarks>
public sealed class Animal : Entity<Guid>
{
    private const int MaxIdentifierLength = 60;

    // Parameterless constructor for EF Core materialization.
    private Animal() : base(Guid.Empty) => Identifier = default!;

    private Animal(Guid id, string identifier, AnimalSex sex, decimal? weightGrams)
        : base(id)
    {
        Identifier = identifier;
        Sex = sex;
        WeightGrams = weightGrams;
    }

    /// <summary>Operator-facing identifier (ear tag / cage code), unique within the project.</summary>
    public string Identifier { get; private set; }

    /// <summary>Biological sex of the animal.</summary>
    public AnimalSex Sex { get; private set; }

    /// <summary>Optional baseline body weight in grams; null when not recorded.</summary>
    public decimal? WeightGrams { get; private set; }

    /// <summary>Creates an animal, guarding a present identifier and a non-negative weight when supplied.</summary>
    public static Animal Create(string identifier, AnimalSex sex, decimal? weightGrams = null)
    {
        Guard.AgainstNullOrWhiteSpace(identifier, nameof(identifier));
        string trimmedIdentifier = identifier.Trim();
        Guard.AgainstMaxLength(trimmedIdentifier, MaxIdentifierLength, nameof(identifier));

        if (weightGrams is { } weight && weight < 0)
            throw new DomainException($"Animal weight cannot be negative. Received: {weight}.");

        return new Animal(Guid.NewGuid(), trimmedIdentifier, sex, weightGrams);
    }
}
