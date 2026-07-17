using SISLAB.Modules.Experiments.Domain.Projects.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// Aggregate root for the in vivo experimental design (decision card [E11] #73, discovery decision F1): the
/// delineation <c>Project → Batch (leva) → Group (dose) → Animal</c>. A project owns its identity, tenant,
/// <see cref="Name"/>, the animal <see cref="Species"/> under study, an optional <see cref="Description"/>, its
/// lifecycle <see cref="ProjectStatus"/>, the design-version counter and the ordered collection of
/// <see cref="Batch"/>es with their groups and animals.
/// </summary>
/// <remarks>
/// <para>
/// <b>Versioning (decision F1).</b> The delineation is versioned <i>per batch</i>: each <see cref="Batch"/> is
/// created pinned to the project's <see cref="CurrentDesignVersion"/> and freezes its design when started, so a
/// running/completed batch is a stable, reproducible cohort. Evolving the design is an explicit
/// <see cref="ReviseDesign"/> that bumps the version; new batches then run the revised design while old batches
/// keep the version they ran.
/// </para>
/// <para>
/// <b>Ids by value.</b> An <see cref="Experiments.Experiment"/> references a project and a batch only by their
/// <see cref="Entity{TId}.Id"/> (no cross-aggregate FK/navigation), exactly like the module's other ids-by-value.
/// The whole batch/group/animal tree is one aggregate: it is mutated only through this root, keeping the
/// "unique animal identifier within a project" and "design frozen once running" invariants in one place.
/// </para>
/// </remarks>
public sealed class Project : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxNameLength = 200;
    private const int MaxSpeciesLength = 120;
    private const int MaxDescriptionLength = 2000;

    private readonly List<Batch> _batches = [];

    // Parameterless constructor for EF Core materialization.
    private Project() : base(Guid.Empty)
    {
        Name = default!;
        Species = default!;
    }

    private Project(Guid id, string name, string species, string? description)
        : base(id)
    {
        Name = name;
        Species = species;
        Description = description;
        Status = ProjectStatus.Draft;
        CurrentDesignVersion = 1;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    public string Name { get; private set; }

    /// <summary>The animal species under study (free text, e.g. "Rattus norvegicus (Wistar)").</summary>
    public string Species { get; private set; }

    public string? Description { get; private set; }

    public ProjectStatus Status { get; private set; }

    /// <summary>
    /// The design version new batches are pinned to. Starts at 1 and is bumped by <see cref="ReviseDesign"/> so
    /// batches created afterwards run the revised delineation while earlier batches keep the version they ran.
    /// </summary>
    public int CurrentDesignVersion { get; private set; }

    /// <summary>The batches (levas) of the project.</summary>
    public IReadOnlyList<Batch> Batches => _batches.AsReadOnly();

    /// <summary>
    /// Creates a project in <see cref="ProjectStatus.Draft"/> at design version 1 and raises the creation event.
    /// </summary>
    public static Project Create(string name, string species, string? description = null)
    {
        Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        string trimmedName = name.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxNameLength, nameof(name));

        Guard.AgainstNullOrWhiteSpace(species, nameof(species));
        string trimmedSpecies = species.Trim();
        Guard.AgainstMaxLength(trimmedSpecies, MaxSpeciesLength, nameof(species));

        string? trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null)
            Guard.AgainstMaxLength(trimmedDescription, MaxDescriptionLength, nameof(description));

        var project = new Project(Guid.NewGuid(), trimmedName, trimmedSpecies, trimmedDescription);
        project.RaiseDomainEvent(new ProjectCreatedEvent(project.CompanyId, project.Id, project.Name));
        return project;
    }

    /// <summary>Adds a new batch (leva) pinned to the current design version. Rejected on a closed project.</summary>
    public Batch AddBatch(string name)
    {
        EnsureNotClosed();

        Batch batch = Batch.Create(name, CurrentDesignVersion);
        _batches.Add(batch);
        return batch;
    }

    /// <summary>Adds a dose group to one of the project's batches (only while that batch's design is open).</summary>
    public Group AddGroup(Guid batchId, string name, Dose dose)
        => FindBatch(batchId).AddGroup(name, dose);

    /// <summary>Enrols an animal into a group of a batch, keeping the identifier unique across the whole project.</summary>
    public Animal AddAnimal(Guid batchId, Guid groupId, string identifier, AnimalSex sex, decimal? weightGrams = null)
    {
        EnsureIdentifierIsFreeAcrossProject(identifier);
        return FindBatch(batchId).AddAnimal(groupId, identifier, sex, weightGrams);
    }

    /// <summary>
    /// Starts a batch (freezes its design) and activates the project. Only valid on a non-closed project.
    /// </summary>
    public void StartBatch(Guid batchId)
    {
        EnsureNotClosed();

        Batch batch = FindBatch(batchId);
        batch.Start();

        if (Status == ProjectStatus.Draft)
            Status = ProjectStatus.Active;

        RaiseDomainEvent(new BatchStartedEvent(CompanyId, Id, batch.Id, batch.DesignVersion));
    }

    /// <summary>Marks a running batch as completed.</summary>
    public void CompleteBatch(Guid batchId) => FindBatch(batchId).Complete();

    /// <summary>
    /// Opens a new design version so subsequent batches run a revised delineation (decision F1 — versioned per
    /// batch). Existing batches keep the version they were created with; this only affects batches added after.
    /// </summary>
    public int ReviseDesign()
    {
        if (Status == ProjectStatus.Closed)
            throw new DomainException($"Project '{Name}' is closed; its design cannot be revised.");

        CurrentDesignVersion++;
        return CurrentDesignVersion;
    }

    /// <summary>Closes the project once its study has ended. Every batch must have finished running.</summary>
    public void Close()
    {
        if (Status == ProjectStatus.Closed)
            return;

        if (_batches.Any(batch => batch.Status == BatchStatus.Running))
            throw new DomainException(
                $"Project '{Name}' cannot be closed while it has running batches.");

        Status = ProjectStatus.Closed;
    }

    /// <summary>Loads a batch by id, or throws when it does not belong to the project.</summary>
    public Batch FindBatch(Guid batchId)
        => _batches.FirstOrDefault(batch => batch.Id == batchId)
           ?? throw new NotFoundException($"Batch '{batchId}' was not found in project '{Name}'.");

    private void EnsureNotClosed()
    {
        if (Status == ProjectStatus.Closed)
            throw new DomainException($"Project '{Name}' is closed and cannot be modified.");
    }

    private void EnsureIdentifierIsFreeAcrossProject(string identifier)
    {
        string trimmed = identifier?.Trim() ?? string.Empty;

        bool taken = _batches
            .SelectMany(batch => batch.Groups)
            .SelectMany(group => group.AnimalIdentifiers)
            .Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase));

        if (taken)
            throw new ConflictException(
                $"Animal identifier '{trimmed}' is already used in project '{Name}'.");
    }
}
