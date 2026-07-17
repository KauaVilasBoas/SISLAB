using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// A single step in an experiment's execution flow (decision card #68 — steps as first-class citizens). Each
/// step has an <see cref="Order"/> within the experiment, a <see cref="Kind"/> (its role in the flow), a
/// <see cref="Title"/>, and — once carried out — records the authorship of the hand-off: <see cref="PerformedBy"/>
/// (the actor's identity) and <see cref="PerformedAtUtc"/>, plus optional <see cref="Notes"/>.
/// </summary>
/// <remarks>
/// The per-step authorship is what makes the in vitro "one person generates the data, another calculates"
/// workflow auditable — each step records who did it and when. A step is a child entity of the
/// <see cref="Experiment"/> aggregate, only ever mutated through it (<c>MarkPerformed</c>).
/// </remarks>
public sealed class ExperimentStep : Entity<Guid>
{
    private const int MaxTitleLength = 200;
    private const int MaxNotesLength = 2000;

    // Parameterless constructor for EF Core materialization.
    private ExperimentStep() : base(Guid.Empty) => Title = default!;

    private ExperimentStep(Guid id, int order, ExperimentStepKind kind, string title)
        : base(id)
    {
        Order = order;
        Kind = kind;
        Title = title;
    }

    /// <summary>Zero-based position of the step within the experiment's ordered flow.</summary>
    public int Order { get; private set; }

    /// <summary>Role the step plays in the flow.</summary>
    public ExperimentStepKind Kind { get; private set; }

    /// <summary>Human-readable title (e.g. "Plate design", "Reader import", "Viability calculation").</summary>
    public string Title { get; private set; }

    /// <summary>Actor who performed the step (identity claim), or null while it is pending.</summary>
    public string? PerformedBy { get; private set; }

    /// <summary>Instant (UTC) the step was performed, or null while it is pending.</summary>
    public DateTime? PerformedAtUtc { get; private set; }

    /// <summary>Optional free-text notes recorded when the step was performed.</summary>
    public string? Notes { get; private set; }

    /// <summary>True once the step has been performed.</summary>
    public bool IsPerformed => PerformedAtUtc.HasValue;

    /// <summary>Creates a pending step at the given order/kind/title.</summary>
    public static ExperimentStep Create(int order, ExperimentStepKind kind, string title)
    {
        Guard.AgainstNegative(order, nameof(order));
        Guard.AgainstNullOrWhiteSpace(title, nameof(title));
        string trimmedTitle = title.Trim();
        Guard.AgainstMaxLength(trimmedTitle, MaxTitleLength, nameof(title));

        return new ExperimentStep(Guid.NewGuid(), order, kind, trimmedTitle);
    }

    /// <summary>
    /// Marks the step as performed by <paramref name="performedBy"/> at <paramref name="performedAtUtc"/>, with
    /// optional notes. Idempotent re-marking simply refreshes the authorship — a step may be re-run.
    /// </summary>
    public void MarkPerformed(string performedBy, DateTime performedAtUtc, string? notes = null)
    {
        Guard.AgainstNullOrWhiteSpace(performedBy, nameof(performedBy));

        string? normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes is not null)
            Guard.AgainstMaxLength(normalizedNotes, MaxNotesLength, nameof(notes));

        PerformedBy = performedBy.Trim();
        PerformedAtUtc = performedAtUtc;
        Notes = normalizedNotes;
    }
}
