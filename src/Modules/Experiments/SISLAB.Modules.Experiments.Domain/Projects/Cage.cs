using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A "caixa" (cage) of a <see cref="Batch"/> (SISLAB-03): the <b>physical housing unit</b> animals arrive and live in
/// before randomization (CX1–CX10, each holding a small number of animals — 4 in the current lab, but a parameter,
/// never fixed). It is the unit the operator measures basal/glicemia against <i>before</i> deciding groups, so it must
/// be able to hold animals that carry no <see cref="Group"/> yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why cage owns the animal (not the group).</b> The real in vivo flow is caixa → basal → seleção → grupos: an
/// animal exists in a cage from arrival, and only later (often after induction) is it assigned to a treatment arm.
/// Modelling the cage as the animal's physical parent lets an animal exist without a group, while the treatment
/// <see cref="Group"/> is an optional <i>assignment</i> the aggregate moves the animal into (<see cref="Animal.GroupId"/>,
/// by value). This supports both randomization moments — dividing groups <i>before</i> induction (assign at entry) and
/// <i>after</i> (assign post-basal) — because the moment is a choice, not a fixed rule.
/// </para>
/// <para>
/// A cage is a child entity of the <see cref="Project"/> aggregate, reached through its <see cref="Batch"/>, and only
/// ever mutated through the aggregate. It owns its animals and the invariant that an animal identifier is unique within
/// the cage; the aggregate additionally enforces uniqueness across the whole project.
/// </para>
/// </remarks>
public sealed class Cage : Entity<Guid>
{
    private const int MaxNameLength = 120;

    private readonly List<Animal> _animals = [];

    // Parameterless constructor for EF Core materialization.
    private Cage() : base(Guid.Empty) => Name = default!;

    private Cage(Guid id, string name, int? capacity)
        : base(id)
    {
        Name = name;
        Capacity = capacity;
    }

    /// <summary>Human-readable cage label (e.g. "CX1").</summary>
    public string Name { get; private set; }

    /// <summary>
    /// The number of animals the cage is meant to hold (e.g. 4 in the current lab), or <see langword="null"/> when
    /// left uncapped. A parameter of the study — never a fixed constant — enforced as an upper bound when supplied.
    /// </summary>
    public int? Capacity { get; private set; }

    /// <summary>The animals currently housed in this cage.</summary>
    public IReadOnlyList<Animal> Animals => _animals.AsReadOnly();

    /// <summary>Creates an empty cage (no animals yet) at the given name and optional capacity.</summary>
    internal static Cage Create(string name, int? capacity)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        if (capacity is { } cap && cap < 1)
            throw new DomainException($"Cage capacity must be at least 1 when supplied. Received: {cap}.");

        return new Cage(Guid.NewGuid(), trimmedName, capacity);
    }

    /// <summary>
    /// Houses an animal in the cage, optionally already assigned to a treatment group (the pre-induction flow). Rejects
    /// a duplicate identifier within the cage and a placement beyond the cage's capacity when one is set.
    /// </summary>
    internal Animal AddAnimal(string identifier, AnimalSex sex, decimal? weightGrams, Guid? groupId)
    {
        Animal animal = Animal.Create(identifier, sex, weightGrams, groupId);

        if (_animals.Any(a => string.Equals(a.Identifier, animal.Identifier, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException(
                $"Animal '{animal.Identifier}' is already housed in cage '{Name}'.");

        if (Capacity is { } cap && _animals.Count >= cap)
            throw new DomainException(
                $"Cage '{Name}' is full (capacity {cap}); it cannot house another animal.");

        _animals.Add(animal);
        return animal;
    }

    /// <summary>Whether this cage houses the given animal.</summary>
    internal bool Houses(Guid animalId) => _animals.Any(a => a.Id == animalId);

    /// <summary>Loads a housed animal by id, or throws when it is not in this cage.</summary>
    internal Animal FindAnimal(Guid animalId)
        => _animals.FirstOrDefault(a => a.Id == animalId)
           ?? throw new NotFoundException($"Animal '{animalId}' is not housed in cage '{Name}'.");

    /// <summary>Every animal identifier currently housed in the cage (for project-wide uniqueness checks).</summary>
    internal IEnumerable<string> AnimalIdentifiers => _animals.Select(a => a.Identifier);
}
