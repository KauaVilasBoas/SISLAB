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

    /// <summary>
    /// The experiment's lead responsible (card [E11] — the "bigger chain", full authority over everything in the
    /// experiment), referenced <b>by value</b> via their Lumen user id — never a cross-module FK. Distinct from
    /// <see cref="CreatedBy"/> (the audit actor claim): this is <i>who may edit</i>, and it is null only for
    /// experiments created before responsibility was introduced.
    /// </summary>
    public Guid? ResponsibleUserId { get; private set; }

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

    // ----------------------------------------------------------------------------------------------------
    // Responsibility (card [E11]). Two levels of edit authority, both referenced by value via the Lumen user
    // id: the lead responsible for the whole experiment and one-or-more responsibles per step. Membership of a
    // user in the active company is validated in the application layer (through the Identity Contracts) before
    // these are called — the aggregate only owns the invariant "who may edit".
    // ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Sets the experiment's lead responsible (full authority). Replaces any previous lead. The application layer
    /// must have validated that <paramref name="userId"/> belongs to the active company before calling.
    /// </summary>
    public void AssignResponsible(Guid userId)
    {
        Guard.AgainstEmptyGuid(userId, nameof(userId));
        ResponsibleUserId = userId;
    }

    /// <summary>Adds <paramref name="userId"/> as a responsible of the step identified by <paramref name="stepId"/>.</summary>
    public void AssignStepResponsible(Guid stepId, Guid userId)
        => RequireStep(stepId).AssignResponsible(userId);

    /// <summary>Removes <paramref name="userId"/> from the responsibles of the step identified by <paramref name="stepId"/>.</summary>
    public void RemoveStepResponsible(Guid stepId, Guid userId)
        => RequireStep(stepId).RemoveResponsible(userId);

    /// <summary>
    /// True once <i>any</i> responsibility has been configured on the experiment (a lead, or at least one step
    /// responsible). While nothing is configured, the responsibility gate is dormant — see
    /// <see cref="CanBeEditedBy"/>.
    /// </summary>
    public bool HasResponsibilityConfigured
        => ResponsibleUserId is not null || _steps.Any(step => step.ResponsibleUserIds.Count > 0);

    /// <summary>
    /// The core edit-authorization rule (card [E11]): a user may edit when they are the experiment's lead
    /// responsible <b>or</b> — for a step-scoped edit — a responsible of that specific step. The lead has authority
    /// over everything; a step responsible only over their step. This is <i>data/ownership</i> authorization,
    /// complementary to Lumen's <c>[RequirePermission]</c> — it never replaces it.
    /// </summary>
    /// <remarks>
    /// Backward-compatibility: when the experiment has <b>no</b> responsibility configured at all
    /// (<see cref="HasResponsibilityConfigured"/> is false — e.g. created before this feature), the gate is
    /// dormant and edits fall through to Lumen's permission gate. It only starts constraining once someone is
    /// designated, so introducing responsibility never locks anyone out of pre-existing experiments.
    /// </remarks>
    /// <param name="userId">The user attempting the edit (their Lumen user id).</param>
    /// <param name="stepId">The step being edited, or null for an experiment-wide edit (only the lead may do it).</param>
    public bool CanBeEditedBy(Guid userId, Guid? stepId = null)
    {
        if (!HasResponsibilityConfigured)
            return true;

        if (userId == Guid.Empty)
            return false;

        if (ResponsibleUserId == userId)
            return true;

        return stepId is { } id
            && _steps.FirstOrDefault(step => step.Id == id) is { } step
            && step.IsResponsible(userId);
    }

    /// <summary>
    /// Guard form of <see cref="CanBeEditedBy"/>: throws <see cref="ForbiddenException"/> (mapped to HTTP 403)
    /// when the user is neither the lead responsible nor a responsible of the given step.
    /// </summary>
    public void EnsureCanBeEditedBy(Guid userId, Guid? stepId = null)
    {
        if (!CanBeEditedBy(userId, stepId))
            throw new ForbiddenException(
                $"You are not a responsible of experiment '{Title}' and cannot edit it.");
    }

    /// <summary>
    /// Guard form scoped to a step identified by its <paramref name="kind"/> (the first step of that kind in the
    /// flow): the lead responsible or a responsible of that step may proceed. Used by the write commands that act
    /// on a specific stage of the flow (design → the Baseline step, import → Measurement, calculate → Calculation).
    /// </summary>
    public void EnsureCanBeEditedBy(Guid userId, ExperimentStepKind kind)
        => EnsureCanBeEditedBy(userId, FindStep(kind)?.Id);

    private ExperimentStep RequireStep(Guid stepId)
    {
        Guard.AgainstEmptyGuid(stepId, nameof(stepId));

        return _steps.FirstOrDefault(step => step.Id == stepId)
            ?? throw new DomainException($"Step '{stepId}' does not belong to experiment '{Title}'.");
    }
}
