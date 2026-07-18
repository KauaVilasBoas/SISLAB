using SISLAB.Modules.Experiments.Domain.Experiments.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Aggregate root for every laboratory assay (decision card #68 — a single central <c>Experiment</c> aggregate;
/// each assay type is a subtype via EF Core TPH). It owns what is common to any experiment: identity, tenant,
/// title/description, its <see cref="ExperimentType"/> discriminator, the lifecycle <see cref="ExperimentStatus"/>
/// with its transition policy, the ordered collection of <see cref="ExperimentStep"/>s, and creation metadata.
/// </summary>
/// <remarks>
/// <para>
/// Behaviour that varies by type (the default step flow and the versioned calculation) does <b>not</b> live on
/// the aggregate — it is a Strategy (<c>IExperimentProtocol</c>) resolved by <see cref="Type"/> in the
/// application layer. The aggregate keeps only the invariants that hold for <i>every</i> experiment, so it stays
/// clean and testable and new assay types are added without touching it (decision card #68: herança para estado,
/// composição/Strategy para comportamento).
/// </para>
/// <para>
/// Status transitions are a domain rule, not free-form assignment: only the moves declared in
/// <see cref="AllowedTransitions"/> are accepted. The <see cref="AwaitingAnalysis"/> state materialises the in
/// vitro hand-off — the calculation ran and produced a snapshot; a human still signs off the analysis.
/// </para>
/// </remarks>
public abstract class Experiment : AggregateRoot<Guid>, ITenantEntity
{
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxCreatedByLength = 200;

    /// <summary>
    /// The only status moves the domain accepts. An experiment is designed (Draft), executed (InProgress),
    /// sits AwaitingAnalysis once its calculation produced a snapshot, is Completed on sign-off, and may be
    /// Archived from any non-terminal state.
    /// </summary>
    private static readonly IReadOnlyDictionary<ExperimentStatus, IReadOnlySet<ExperimentStatus>>
        AllowedTransitions = new Dictionary<ExperimentStatus, IReadOnlySet<ExperimentStatus>>
        {
            [ExperimentStatus.Draft] = new HashSet<ExperimentStatus>
            {
                ExperimentStatus.InProgress,
                ExperimentStatus.Archived,
            },
            [ExperimentStatus.InProgress] = new HashSet<ExperimentStatus>
            {
                ExperimentStatus.AwaitingAnalysis,
                ExperimentStatus.AwaitingCalculation,
                ExperimentStatus.Completed,
                ExperimentStatus.Archived,
            },
            [ExperimentStatus.AwaitingAnalysis] = new HashSet<ExperimentStatus>
            {
                ExperimentStatus.Completed,
                ExperimentStatus.Archived,
            },
            [ExperimentStatus.AwaitingCalculation] = new HashSet<ExperimentStatus>
            {
                ExperimentStatus.AwaitingAnalysis,
                ExperimentStatus.Completed,
                ExperimentStatus.Archived,
            },
            [ExperimentStatus.Completed] = new HashSet<ExperimentStatus>
            {
                ExperimentStatus.Archived,
            },
            [ExperimentStatus.Archived] = new HashSet<ExperimentStatus>(),
        };

    private readonly List<ExperimentStep> _steps = [];

    // Parameterless constructor for EF Core materialization.
    protected Experiment() : base(Guid.Empty)
    {
        Title = default!;
        CreatedBy = default!;
    }

    protected Experiment(
        Guid id,
        ExperimentType type,
        string title,
        string? description,
        string createdBy,
        DateTime createdAtUtc)
        : base(id)
    {
        Type = type;
        Title = title;
        Description = description;
        Status = ExperimentStatus.Draft;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    /// <inheritdoc />
    public Guid CompanyId { get; private init; }

    /// <summary>Assay type discriminator (EF Core TPH).</summary>
    public ExperimentType Type { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public ExperimentStatus Status { get; private set; }

    /// <summary>Actor who created the experiment (identity claim).</summary>
    public string CreatedBy { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The ordered execution flow of this experiment.</summary>
    public IReadOnlyList<ExperimentStep> Steps => _steps.OrderBy(step => step.Order).ToList().AsReadOnly();

    /// <summary>Adds a step to the experiment's flow (used by the type's protocol when seeding the defaults).</summary>
    protected void AddStep(ExperimentStep step)
    {
        Guard.AgainstNull(step, nameof(step));
        _steps.Add(step);
    }

    /// <summary>Finds a step by its kind (the first, by order), or null when the flow has none.</summary>
    public ExperimentStep? FindStep(ExperimentStepKind kind)
        => _steps.OrderBy(step => step.Order).FirstOrDefault(step => step.Kind == kind);

    /// <summary>
    /// Finds a step by its kind and title (case-insensitive), or null when the flow has none. Used by the in vivo
    /// timepoint launch to locate the specific timepoint step by its label.
    /// </summary>
    public ExperimentStep? FindStep(ExperimentStepKind kind, string title)
        => _steps
            .OrderBy(step => step.Order)
            .FirstOrDefault(step =>
                step.Kind == kind
                && string.Equals(step.Title, title.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Moves the experiment into execution. Only valid from <see cref="ExperimentStatus.Draft"/>.</summary>
    public void Start() => TransitionTo(ExperimentStatus.InProgress);

    /// <summary>Signs off the experiment as complete.</summary>
    public void Complete() => TransitionTo(ExperimentStatus.Completed);

    /// <summary>Retires the experiment from the active list.</summary>
    public void Archive() => TransitionTo(ExperimentStatus.Archived);

    /// <summary>
    /// Moves the experiment to <paramref name="target"/>, enforcing the transition policy. Moving to the current
    /// status is a no-op; an unsupported move is rejected with a domain error.
    /// </summary>
    protected void TransitionTo(ExperimentStatus target)
    {
        if (target == Status)
            return;

        if (!AllowedTransitions[Status].Contains(target))
            throw new DomainException($"Experiment '{Title}' cannot move from {Status} to {target}.");

        Status = target;
    }

    /// <summary>Guards and normalizes the common creation fields shared by every subtype's factory.</summary>
    protected static (string Title, string? Description, string CreatedBy) NormalizeCreation(
        string title,
        string? description,
        string createdBy)
    {
        Guard.AgainstNullOrWhiteSpace(title, nameof(title));
        string trimmedTitle = title.Trim();
        Guard.AgainstMaxLength(trimmedTitle, MaxTitleLength, nameof(title));

        string? trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription is not null)
            Guard.AgainstMaxLength(trimmedDescription, MaxDescriptionLength, nameof(description));

        Guard.AgainstNullOrWhiteSpace(createdBy, nameof(createdBy));
        string trimmedCreatedBy = createdBy.Trim();
        Guard.AgainstMaxLength(trimmedCreatedBy, MaxCreatedByLength, nameof(createdBy));

        return (trimmedTitle, trimmedDescription, trimmedCreatedBy);
    }

    /// <summary>Raises the creation event; called by the subtype factory once the aggregate is built.</summary>
    protected void RaiseCreated()
        => RaiseDomainEvent(new ExperimentCreatedEvent(CompanyId, Id, Type, Title));
}
