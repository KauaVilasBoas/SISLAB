using SISLAB.Modules.Experiments.Domain.Projects.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// Aggregate root for the in vivo experimental design (decision card [E11] #73, discovery decision F1, SISLAB-03):
/// the delineation <c>Project → Batch (leva) → Cage (caixa) → Animal</c>, with <c>Group (dose)</c> an <b>optional
/// assignment</b> on the animal (by value) rather than its owner. A project owns its identity, tenant,
/// <see cref="Name"/>, the animal <see cref="Species"/> under study, an optional <see cref="Description"/>, its
/// lifecycle <see cref="ProjectStatus"/>, the design-version counter and the ordered collection of
/// <see cref="Batch"/>es with their cages, groups and animals.
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
    private readonly List<PhysiologicalReading> _physiologicalReadings = [];

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
    /// The recurring physiological readings (glicemia/peso, …) taken on the project's animals across timepoints
    /// (SISLAB-02). Held on the root so the animal-selection criterion can evaluate them together with the animals
    /// they belong to, within a single aggregate.
    /// </summary>
    public IReadOnlyList<PhysiologicalReading> PhysiologicalReadings => _physiologicalReadings.AsReadOnly();

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

    /// <summary>
    /// Adds a cage (caixa) to one of the project's batches (SISLAB-03), with an optional capacity (e.g. 4 — a
    /// parameter, never fixed). Only valid while that batch's design is open.
    /// </summary>
    public Cage AddCage(Guid batchId, string name, int? capacity = null)
        => FindBatch(batchId).AddCage(name, capacity);

    /// <summary>
    /// Houses an animal in a cage of a batch (SISLAB-03), keeping the identifier unique across the whole project. The
    /// treatment <paramref name="groupId"/> is <b>optional</b>: pass <see langword="null"/> for the pre-randomization
    /// flow (animal exists in a cage without a group and is assigned later), or a group id to assign it at entry (the
    /// classic "divide groups before induction" flow). The group, when supplied, must belong to the same batch.
    /// </summary>
    public Animal AddAnimalToCage(
        Guid batchId,
        Guid cageId,
        string identifier,
        AnimalSex sex,
        decimal? weightGrams = null,
        Guid? groupId = null)
    {
        EnsureIdentifierIsFreeAcrossProject(identifier);
        return FindBatch(batchId).AddAnimalToCage(cageId, identifier, sex, weightGrams, groupId);
    }

    /// <summary>
    /// Assigns (or moves) an animal to a treatment group (SISLAB-03) after basal/induction — including redistributing a
    /// discrepant cage across groups. Only valid while the batch's design is open (assignment locks once the leva
    /// starts). The group must belong to the same batch as the animal.
    /// </summary>
    public void AssignAnimalToGroup(Guid batchId, Guid animalId, Guid groupId)
    {
        EnsureNotClosed();
        FindBatch(batchId).AssignAnimalToGroup(animalId, groupId);
    }

    /// <summary>Removes an animal's group assignment (back to unassigned). Only valid while the batch's design is open.</summary>
    public void UnassignAnimalFromGroup(Guid batchId, Guid animalId)
    {
        EnsureNotClosed();
        FindBatch(batchId).UnassignAnimalFromGroup(animalId);
    }

    /// <summary>
    /// Records a recurring physiological reading (glicemia/peso, …) on one of the project's animals at a timepoint
    /// (SISLAB-02). The animal must belong to the project (guarded here); the parameter code, value, unit and
    /// timepoint are supplied by the caller — nothing lab-specific is fixed by the aggregate. Allowed on any
    /// non-closed project (readings are collected while the study runs, not part of the frozen design). Returns the
    /// new reading.
    /// </summary>
    public PhysiologicalReading RecordPhysiologicalReading(
        Guid animalId,
        string parameterCode,
        decimal value,
        string unit,
        string timepointLabel,
        string recordedBy,
        DateTime recordedAtUtc)
    {
        EnsureNotClosed();
        EnsureAnimalBelongsToProject(animalId);

        PhysiologicalReading reading = PhysiologicalReading.Create(
            animalId, parameterCode, value, unit, timepointLabel, recordedBy, recordedAtUtc);

        _physiologicalReadings.Add(reading);
        return reading;
    }

    /// <summary>
    /// Applies the inclusion criteria (SISLAB-02) to the animals of one batch, marking each included/excluded and
    /// recording the deciding parameter, value and reason on the animal (<see cref="Animal.Inclusion"/>). Selection
    /// is per batch ("leva") because the experimental model — and therefore which parameters apply — is bound per
    /// batch (SISLAB-04); the model's <paramref name="applicableParameters"/> gate which criteria run: a criterion
    /// whose parameter is <b>not</b> applicable is skipped, so it never blocks or excludes an animal (glicemia is
    /// simply ignored for a non-diabetic model). A criterion that applies but has no reading for an animal leaves
    /// that animal's prior decision untouched (nothing to decide on).
    /// </summary>
    /// <remarks>
    /// The criteria and applicable-parameter set arrive as domain-local abstractions (<see cref="IInclusionRule"/>
    /// and a plain code set), adapted in the application layer from the Configuration Contracts — the aggregate
    /// depends on no other module. The latest reading (by instant) for the parameter is the deciding one, so a
    /// re-measured animal is re-selected on its newest value. Allowed on any non-closed project.
    /// </remarks>
    /// <returns>The number of animals for which a decision was taken (i.e. had an applicable reading).</returns>
    public int ApplyInclusionCriteria(
        Guid batchId,
        IEnumerable<IInclusionRule> criteria,
        IReadOnlySet<string> applicableParameters)
    {
        Guard.AgainstNull(criteria, nameof(criteria));
        Guard.AgainstNull(applicableParameters, nameof(applicableParameters));
        EnsureNotClosed();

        Batch batch = FindBatch(batchId);

        // Only criteria whose parameter the model declares applicable participate — an inapplicable parameter is
        // non-blocking (critério de aceite: "parâmetro inaplicável ao modelo não bloqueia").
        List<IInclusionRule> applicableRules = criteria
            .Where(rule => applicableParameters.Contains(rule.ParameterCode, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (applicableRules.Count == 0)
            return 0;

        int decided = 0;

        IEnumerable<Animal> batchAnimals = batch.Animals;

        foreach (Animal animal in batchAnimals)
        {
            foreach (IInclusionRule rule in applicableRules)
            {
                PhysiologicalReading? reading = LatestReadingFor(animal.Id, rule.ParameterCode);
                if (reading is null)
                    continue;

                bool qualified = rule.QualifiedBy(reading.Value);
                string reason = rule.Describe(reading.Value, qualified);

                animal.RecordInclusion(qualified
                    ? InclusionDecision.Included(rule.ParameterCode, reading.Value, reason)
                    : InclusionDecision.Excluded(rule.ParameterCode, reading.Value, reason));

                decided++;
            }
        }

        return decided;
    }

    /// <summary>The latest (by instant) reading of <paramref name="parameterCode"/> for an animal, or null.</summary>
    private PhysiologicalReading? LatestReadingFor(Guid animalId, string parameterCode)
        => _physiologicalReadings
            .Where(reading => reading.AnimalId == animalId && reading.IsForParameter(parameterCode))
            .OrderByDescending(reading => reading.RecordedAtUtc)
            .FirstOrDefault();

    /// <summary>
    /// Binds one of the project's batches to an experimental model (SISLAB-04), referenced by value. Only valid on a
    /// non-closed project and while that batch's design is open (enforced by the batch). The model's existence and
    /// tenant ownership are validated in the application layer through the Configuration Contracts port before this
    /// call — the aggregate only guards the design-open invariant and a non-empty id.
    /// </summary>
    public void BindBatchToModel(Guid batchId, Guid experimentalModelId)
    {
        EnsureNotClosed();
        FindBatch(batchId).BindExperimentalModel(experimentalModelId);
    }

    /// <summary>Clears a batch's experimental-model binding. Only valid while the batch's design is open.</summary>
    public void ClearBatchModel(Guid batchId)
    {
        EnsureNotClosed();
        FindBatch(batchId).ClearExperimentalModel();
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

    /// <summary>Every animal enrolled anywhere in the project (across its batches and cages).</summary>
    private IEnumerable<Animal> AllAnimals =>
        _batches.SelectMany(batch => batch.Animals);

    private void EnsureAnimalBelongsToProject(Guid animalId)
    {
        if (AllAnimals.All(animal => animal.Id != animalId))
            throw new NotFoundException(
                $"Animal '{animalId}' is not enrolled in project '{Name}'.");
    }

    private void EnsureIdentifierIsFreeAcrossProject(string identifier)
    {
        string trimmed = identifier?.Trim() ?? string.Empty;

        bool taken = _batches
            .SelectMany(batch => batch.AnimalIdentifiers)
            .Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase));

        if (taken)
            throw new ConflictException(
                $"Animal identifier '{trimmed}' is already used in project '{Name}'.");
    }
}
