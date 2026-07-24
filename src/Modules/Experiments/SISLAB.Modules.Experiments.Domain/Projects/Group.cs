using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A treatment arm of a <see cref="Batch"/> (card [E11] #73) — the "grupo (dose)": the set of animals that
/// receive the same <see cref="Dose"/>. Groups are the columns of the classic in vivo delineation (vehicle /
/// control at dose 0, then ascending doses), and are the unit the Prism export aggregates by (group × timepoint).
/// </summary>
/// <remarks>
/// <para>
/// A group is a child entity of the <see cref="Project"/> aggregate, reached through its <see cref="Batch"/>, and
/// only ever mutated through the aggregate. Since SISLAB-03 the group is a pure <b>treatment definition</b> (name +
/// dose): it no longer <i>owns</i> animals — animals are housed in a <see cref="Cage"/> and reference their group by
/// value (<see cref="Animal.GroupId"/>). This lets an animal exist before it is assigned to any group.
/// </para>
/// </remarks>
public sealed class Group : Entity<Guid>
{
    private const int MaxNameLength = 120;

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

    /// <summary>Creates a group (a treatment definition) at the given name and dose.</summary>
    internal static Group Create(string name, Dose dose)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Guard.AgainstNull(dose, nameof(dose));

        return new Group(Guid.NewGuid(), trimmedName, dose);
    }
}
