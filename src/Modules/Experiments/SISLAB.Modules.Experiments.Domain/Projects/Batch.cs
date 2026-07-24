using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A "leva" (batch/cohort) of a <see cref="Project"/> (card [E11] #73): the animals housed in <see cref="Cage"/>s
/// and the treatment <see cref="Group"/>s (doses) they may be assigned to, run together on one schedule. The batch is
/// the unit that <b>fixes the design version</b> — decision F1: the delineation is versionable per batch, so a change
/// to the project design bumps a new batch's <see cref="DesignVersion"/> while a running or completed batch keeps the
/// exact design it ran.
/// </summary>
/// <remarks>
/// <para>
/// A batch is a child entity of the <see cref="Project"/> aggregate and is only mutated through it. Its design —
/// cages, groups and group assignments — may only be edited while it is <see cref="BatchStatus.Planned"/>; once it is
/// <see cref="Start">started</see> the design is frozen, so randomization/assignment is locked after the leva starts
/// (SISLAB-03). This is what makes the per-batch versioning meaningful (a started batch is a stable, reproducible
/// cohort). An <see cref="Experiment"/> references a batch <b>by value</b> (its <see cref="Entity{TId}.Id"/>), never by
/// navigation, honouring the module's ids-by-value rule.
/// </para>
/// </remarks>
public sealed class Batch : Entity<Guid>
{
    private const int MaxNameLength = 120;

    private readonly List<Cage> _cages = [];
    private readonly List<Group> _groups = [];

    // Parameterless constructor for EF Core materialization.
    private Batch() : base(Guid.Empty) => Name = default!;

    private Batch(Guid id, string name, int designVersion)
        : base(id)
    {
        Name = name;
        DesignVersion = designVersion;
        Status = BatchStatus.Planned;
    }

    /// <summary>Human-readable batch label (e.g. "Leva 1 — Jan/2026").</summary>
    public string Name { get; private set; }

    /// <summary>
    /// The design version this batch runs. Set once at creation from the project's current version and never
    /// changed, so the batch is a stable snapshot of the delineation (decision F1 — versioned per batch).
    /// </summary>
    public int DesignVersion { get; private set; }

    /// <summary>Lifecycle state of the batch.</summary>
    public BatchStatus Status { get; private set; }

    /// <summary>
    /// The experimental model / induction protocol (SISLAB-04) this batch runs, referenced <b>by value</b> — the
    /// id of a <c>Configuration.ExperimentalModel</c> owned by the Configuration bounded context (no cross-module
    /// FK/navigation, honouring the ids-by-value rule). <see langword="null"/> while the batch has no model bound.
    /// The model parameterizes which timepoints/parameters/groups apply; it can only be set or changed while the
    /// design is open (planned), so a started batch is a stable, reproducible cohort of exactly one model version.
    /// </summary>
    public Guid? ExperimentalModelId { get; private set; }

    /// <summary>The physical housing units (caixas) of this batch (SISLAB-03), each holding its animals.</summary>
    public IReadOnlyList<Cage> Cages => _cages.AsReadOnly();

    /// <summary>The treatment arms (dose groups) of this batch. Animals reference a group by value, not by ownership.</summary>
    public IReadOnlyList<Group> Groups => _groups.AsReadOnly();

    /// <summary>Every animal of the batch, housed across its cages (regardless of group assignment).</summary>
    public IEnumerable<Animal> Animals => _cages.SelectMany(cage => cage.Animals);

    /// <summary>Creates a planned batch pinned to the supplied design version.</summary>
    internal static Batch Create(string name, int designVersion)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        if (designVersion < 1)
            throw new DomainException($"Design version must be at least 1. Received: {designVersion}.");

        return new Batch(Guid.NewGuid(), trimmedName, designVersion);
    }

    /// <summary>Adds a dose group to the batch. Allowed only while the batch is still planned (design open).</summary>
    public Group AddGroup(string name, Dose dose)
    {
        EnsureDesignOpen();

        Group group = Group.Create(name, dose);
        _groups.Add(group);
        return group;
    }

    /// <summary>
    /// Adds a cage (caixa) to the batch (SISLAB-03), with an optional capacity (e.g. 4 — a parameter, not fixed).
    /// Allowed only while the design is open (planned).
    /// </summary>
    public Cage AddCage(string name, int? capacity = null)
    {
        EnsureDesignOpen();

        Cage cage = Cage.Create(name, capacity);
        _cages.Add(cage);
        return cage;
    }

    /// <summary>
    /// Binds this batch to an experimental model (SISLAB-04), referenced by value. Allowed only while the design is
    /// open (planned) — the model choice is part of the delineation, so it freezes when the batch starts, keeping
    /// the cohort reproducible. Re-binding to a different model while planned is allowed (correcting the design);
    /// pass <see cref="Guid.Empty"/> to reject an unset id at the boundary.
    /// </summary>
    public void BindExperimentalModel(Guid experimentalModelId)
    {
        EnsureDesignOpen();

        if (experimentalModelId == Guid.Empty)
            throw new DomainException("An experimental model id is required to bind a batch to a model.");

        ExperimentalModelId = experimentalModelId;
    }

    /// <summary>
    /// Clears the batch's experimental-model binding. Allowed only while the design is open (planned), for the same
    /// reproducibility reason as <see cref="BindExperimentalModel"/>.
    /// </summary>
    public void ClearExperimentalModel()
    {
        EnsureDesignOpen();
        ExperimentalModelId = null;
    }

    /// <summary>
    /// Houses an animal in one of the batch's cages (SISLAB-03), optionally already assigned to a group — the
    /// pre-randomization flow adds it with no group; the classic flow assigns the group at entry. Allowed only while
    /// the design is open. The identifier's project-wide uniqueness is enforced by the aggregate root before this call.
    /// </summary>
    internal Animal AddAnimalToCage(Guid cageId, string identifier, AnimalSex sex, decimal? weightGrams, Guid? groupId)
    {
        EnsureDesignOpen();

        Cage cage = FindCage(cageId);

        if (groupId is { } assignedGroup)
            EnsureGroupExists(assignedGroup);

        return cage.AddAnimal(identifier, sex, weightGrams, groupId);
    }

    /// <summary>
    /// Assigns (or moves) an animal to a treatment group (SISLAB-03), regardless of which cage houses it — this is how
    /// a discrepant cage is redistributed after basal/induction. Allowed only while the design is open.
    /// </summary>
    internal void AssignAnimalToGroup(Guid animalId, Guid groupId)
    {
        EnsureDesignOpen();
        EnsureGroupExists(groupId);

        FindAnimal(animalId).AssignToGroup(groupId);
    }

    /// <summary>Removes an animal's group assignment (back to unassigned). Allowed only while the design is open.</summary>
    internal void UnassignAnimalFromGroup(Guid animalId)
    {
        EnsureDesignOpen();
        FindAnimal(animalId).Unassign();
    }

    /// <summary>
    /// Starts the batch: freezes its design and moves it to <see cref="BatchStatus.Running"/>. Requires at least one
    /// animal housed — an empty design cannot be run.
    /// </summary>
    public void Start()
    {
        if (Status != BatchStatus.Planned)
            throw new DomainException($"Batch '{Name}' can only be started while it is planned.");

        if (!Animals.Any())
            throw new DomainException(
                $"Batch '{Name}' cannot be started: it must house at least one animal.");

        Status = BatchStatus.Running;
    }

    /// <summary>Marks the batch's schedule as finished. Only valid for a running batch.</summary>
    public void Complete()
    {
        if (Status != BatchStatus.Running)
            throw new DomainException($"Batch '{Name}' can only be completed while it is running.");

        Status = BatchStatus.Completed;
    }

    /// <summary>Total number of animals housed across the batch's cages.</summary>
    public int AnimalCount => _cages.Sum(cage => cage.Animals.Count);

    /// <summary>Loads a cage by id, or throws when it does not belong to the batch.</summary>
    internal Cage FindCage(Guid cageId)
        => _cages.FirstOrDefault(cage => cage.Id == cageId)
           ?? throw new NotFoundException($"Cage '{cageId}' was not found in batch '{Name}'.");

    /// <summary>Loads an animal by id from whichever cage houses it, or throws when the batch has no such animal.</summary>
    internal Animal FindAnimal(Guid animalId)
        => _cages.SelectMany(cage => cage.Animals).FirstOrDefault(animal => animal.Id == animalId)
           ?? throw new NotFoundException($"Animal '{animalId}' was not found in batch '{Name}'.");

    /// <summary>Every animal identifier currently housed in the batch (for uniqueness checks).</summary>
    internal IEnumerable<string> AnimalIdentifiers => _cages.SelectMany(cage => cage.AnimalIdentifiers);

    /// <summary>Loads a dose group by id, or throws when it does not belong to the batch.</summary>
    internal Group FindGroup(Guid groupId)
        => _groups.FirstOrDefault(group => group.Id == groupId)
           ?? throw new NotFoundException($"Group '{groupId}' was not found in batch '{Name}'.");

    private void EnsureGroupExists(Guid groupId)
    {
        if (_groups.All(group => group.Id != groupId))
            throw new NotFoundException($"Group '{groupId}' was not found in batch '{Name}'.");
    }

    private void EnsureDesignOpen()
    {
        if (Status != BatchStatus.Planned)
            throw new DomainException(
                $"Batch '{Name}' design is frozen (status {Status}); it can only be edited while planned.");
    }
}
