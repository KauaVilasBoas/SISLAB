using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Configuration.Domain.ExperimentalModels;

/// <summary>
/// The induction protocol of an experimental model (SISLAB-04): how many times the induction agent is administered
/// and the spacing between administrations — the spreadsheet's ND model with a 1st/2nd induction and a reference
/// readout on the 28th day ("três dias consecutivos... após 28 dias"). An immutable value object owning the timing
/// invariants, so a model can never hold a nonsensical protocol (zero administrations, negative spacing).
/// </summary>
/// <remarks>
/// <para>
/// <b>Administrations and interval.</b> <see cref="Administrations"/> is the number of induction doses (≥ 1);
/// <see cref="IntervalDays"/> is the number of days between consecutive administrations (≥ 0 — same-day/consecutive
/// protocols use small values). A single-administration protocol has no interval, so the interval must be 0 there.
/// </para>
/// <para>
/// <b>Reference day.</b> <see cref="ReferenceDayAfterInduction"/> records the day, counted from the first
/// induction, when the model's reference readout/selection happens (28 in the ND example). It is what SISLAB-10
/// will later anchor the generated schedule on; here it is stored declaratively as part of the protocol.
/// </para>
/// Nothing here is lab-specific: the counts, spacing and reference day are all cadastered per model.
/// </remarks>
public sealed class InductionProtocol : ValueObject
{
    private const int MaxAdministrations = 100;
    private const int MaxIntervalDays = 3650;
    private const int MaxReferenceDay = 3650;

    private InductionProtocol(int administrations, int intervalDays, int referenceDayAfterInduction)
    {
        Administrations = administrations;
        IntervalDays = intervalDays;
        ReferenceDayAfterInduction = referenceDayAfterInduction;
    }

    /// <summary>Number of induction administrations (≥ 1).</summary>
    public int Administrations { get; }

    /// <summary>Days between consecutive administrations (≥ 0; must be 0 for a single-administration protocol).</summary>
    public int IntervalDays { get; }

    /// <summary>Day (from the first induction) of the model's reference readout/selection — e.g. 28 in the ND model.</summary>
    public int ReferenceDayAfterInduction { get; }

    /// <summary>Builds the protocol, enforcing the administration/interval/reference-day invariants.</summary>
    public static InductionProtocol Of(int administrations, int intervalDays, int referenceDayAfterInduction)
    {
        if (administrations < 1)
            throw new DomainException("An induction protocol requires at least one administration.");

        if (administrations > MaxAdministrations)
            throw new DomainException($"An induction protocol cannot exceed {MaxAdministrations} administrations.");

        if (intervalDays < 0)
            throw new DomainException("The interval between administrations cannot be negative.");

        if (intervalDays > MaxIntervalDays)
            throw new DomainException($"The interval between administrations cannot exceed {MaxIntervalDays} days.");

        if (administrations == 1 && intervalDays != 0)
            throw new DomainException("A single-administration protocol cannot define an interval between administrations.");

        if (referenceDayAfterInduction < 0)
            throw new DomainException("The reference day after induction cannot be negative.");

        if (referenceDayAfterInduction > MaxReferenceDay)
            throw new DomainException($"The reference day after induction cannot exceed {MaxReferenceDay} days.");

        return new InductionProtocol(administrations, intervalDays, referenceDayAfterInduction);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Administrations;
        yield return IntervalDays;
        yield return ReferenceDayAfterInduction;
    }
}
