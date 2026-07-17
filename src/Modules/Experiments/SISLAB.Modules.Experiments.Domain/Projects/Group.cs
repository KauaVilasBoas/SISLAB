using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A treatment arm of a <see cref="Batch"/> (card [E11] #73) — the "grupo (dose)": the set of animals that
/// receive the same <see cref="Dose"/>. Groups are the columns of the classic in vivo delineation (vehicle /
/// control at dose 0, then ascending doses), and are the unit the Prism export aggregates by (group × timepoint).
/// </summary>
/// <remarks>
/// A group is a child entity of the <see cref="Project"/> aggregate, reached through its <see cref="Batch"/>, and
/// only ever mutated through the aggregate. It owns its animals and the invariant that an animal identifier is
/// unique within the group; the aggregate additionally enforces uniqueness across the whole project.
/// </remarks>
public sealed class Group : Entity<Guid>
{
    private const int MaxNameLength = 120;

    private readonly List<Animal> _animals = [];

    // Parameterless constructor for EF Core materialization.
    private Group() : base(Guid.Empty)
    {
        Name = default!;
        Dose = default!;
    }

    private Group(Guid id, string name, Dose dose)
        : base(id)
    {
        Name = name;
        Dose = dose;
    }

    /// <summary>Human-readable arm name (e.g. "Controle (veículo)", "Dose 10 mg/kg").</summary>
    public string Name { get; private set; }

    /// <summary>The dose every animal in this arm receives (zero for the vehicle/control arm).</summary>
    public Dose Dose { get; private set; }

    /// <summary>The animals enrolled in this arm.</summary>
    public IReadOnlyList<Animal> Animals => _animals.AsReadOnly();

    /// <summary>Creates an empty group (no animals yet) at the given name and dose.</summary>
    public static Group Create(string name, Dose dose)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Guard.AgainstNull(dose, nameof(dose));

        return new Group(Guid.NewGuid(), trimmedName, dose);
    }

    /// <summary>Enrols an animal in the group, rejecting a duplicate identifier within the group.</summary>
    public Animal AddAnimal(string identifier, AnimalSex sex, decimal? weightGrams = null)
    {
        Animal animal = Animal.Create(identifier, sex, weightGrams);

        if (_animals.Any(a => string.Equals(a.Identifier, animal.Identifier, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException(
                $"Animal '{animal.Identifier}' is already enrolled in group '{Name}'.");

        _animals.Add(animal);
        return animal;
    }

    /// <summary>Every animal identifier currently enrolled in the group (for project-wide uniqueness checks).</summary>
    internal IEnumerable<string> AnimalIdentifiers => _animals.Select(a => a.Identifier);
}
