using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A "leva" (batch/cohort) of a <see cref="Project"/> (card [E11] #73): a set of treatment <see cref="Group"/>s,
/// each with its animals, that are run together on one schedule. The batch is the unit that <b>fixes the design
/// version</b> — decision F1: the delineation is versionable per batch, so a change to the project design bumps a
/// new batch's <see cref="DesignVersion"/> while a running or completed batch keeps the exact design it ran.
/// </summary>
/// <remarks>
/// <para>
/// A batch is a child entity of the <see cref="Project"/> aggregate and is only mutated through it. Its groups may
/// only be edited while it is <see cref="BatchStatus.Planned"/>; once it is <see cref="Start">started</see> the
/// design is frozen — this is what makes the per-batch versioning meaningful (a started batch is a stable,
/// reproducible cohort). An <see cref="Experiment"/> references a batch <b>by value</b> (its <see cref="Entity{TId}.Id"/>),
/// never by navigation, honouring the module's ids-by-value rule.
/// </para>
/// </remarks>
public sealed class Batch : Entity<Guid>
{
    private const int MaxNameLength = 120;

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

    /// <summary>The treatment arms (dose groups) of this batch.</summary>
    public IReadOnlyList<Group> Groups => _groups.AsReadOnly();

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

    /// <summary>Enrols an animal into one of the batch's groups. Allowed only while the design is open.</summary>
    public Animal AddAnimal(Guid groupId, string identifier, AnimalSex sex, decimal? weightGrams = null)
    {
        EnsureDesignOpen();

        Group group = _groups.FirstOrDefault(g => g.Id == groupId)
            ?? throw new NotFoundException($"Group '{groupId}' was not found in batch '{Name}'.");

        EnsureIdentifierIsFree(identifier);

        return group.AddAnimal(identifier, sex, weightGrams);
    }

    /// <summary>
    /// Starts the batch: freezes its design and moves it to <see cref="BatchStatus.Running"/>. Requires at least
    /// one group with at least one animal — an empty design cannot be run.
    /// </summary>
    public void Start()
    {
        if (Status != BatchStatus.Planned)
            throw new DomainException($"Batch '{Name}' can only be started while it is planned.");

        if (_groups.Count == 0 || _groups.All(group => group.Animals.Count == 0))
            throw new DomainException(
                $"Batch '{Name}' cannot be started: it must have at least one group with at least one animal.");

        Status = BatchStatus.Running;
    }

    /// <summary>Marks the batch's schedule as finished. Only valid for a running batch.</summary>
    public void Complete()
    {
        if (Status != BatchStatus.Running)
            throw new DomainException($"Batch '{Name}' can only be completed while it is running.");

        Status = BatchStatus.Completed;
    }

    /// <summary>Total number of animals enrolled across the batch's groups.</summary>
    public int AnimalCount => _groups.Sum(group => group.Animals.Count);

    private void EnsureDesignOpen()
    {
        if (Status != BatchStatus.Planned)
            throw new DomainException(
                $"Batch '{Name}' design is frozen (status {Status}); it can only be edited while planned.");
    }

    private void EnsureIdentifierIsFree(string identifier)
    {
        string trimmed = identifier?.Trim() ?? string.Empty;

        bool taken = _groups
            .SelectMany(group => group.AnimalIdentifiers)
            .Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase));

        if (taken)
            throw new ConflictException(
                $"Animal identifier '{trimmed}' is already used in batch '{Name}'.");
    }
}
