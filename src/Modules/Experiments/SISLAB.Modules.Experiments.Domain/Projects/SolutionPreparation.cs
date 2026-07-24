using SISLAB.Modules.Experiments.Domain.Preparations;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// A traceable, immutable snapshot of an in vivo solution preparation (SISLAB-01), in the spirit of the plate
/// <c>FormulaSnapshot</c>: it freezes the exact inputs the operator supplied (dose, group weight, g:µL relation,
/// compound state/density, vehicle-only flag), the result the versioned <see cref="InVivoPreparationCalculator"/>
/// produced (compound mass/volume, final volume, diluent) and the author + instant, linked to the batch/group it was
/// prepared for. Once confirmed it is never mutated — a preparation is reproducible from its frozen inputs.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a child of the <see cref="Project"/> aggregate.</b> A preparation is prepared <i>for a dose group of a
/// batch</i>, and both the <see cref="Batch"/> and the <see cref="Group"/> are children of the <see cref="Project"/>
/// aggregate. Holding the snapshot on the same root lets the aggregate guarantee "the group/batch exists in this
/// project" as a single-aggregate invariant, and keeps the <see cref="BatchId"/>/<see cref="GroupId"/> as plain
/// by-value references (no cross-aggregate navigation), exactly like <see cref="PhysiologicalReading"/>.
/// </para>
/// <para>
/// <b>Immutability.</b> The snapshot has no behaviour that changes it after creation; the frozen
/// <see cref="InVivoPreparationInput"/> and <see cref="InVivoPreparationResult"/> value objects (compared by value)
/// make it a pure function of its inputs, so re-running the calculator on the same inputs yields the same numbers.
/// The <see cref="FormulaCode"/>/<see cref="FormulaExpression"/> pin the formula version used, so a later formula
/// change never silently reinterprets an old preparation.
/// </para>
/// </remarks>
public sealed class SolutionPreparation : Entity<Guid>
{
    private const int MaxFormulaCodeLength = 60;
    private const int MaxFormulaExpressionLength = 500;
    private const int MaxPreparedByLength = 200;

    // Parameterless constructor for EF Core materialization.
    private SolutionPreparation() : base(Guid.Empty)
    {
        Input = default!;
        Result = default!;
        FormulaCode = default!;
        FormulaExpression = default!;
        PreparedBy = default!;
    }

    private SolutionPreparation(
        Guid id,
        Guid batchId,
        Guid groupId,
        InVivoPreparationInput input,
        InVivoPreparationResult result,
        string formulaCode,
        string formulaExpression,
        string preparedBy,
        DateTime preparedAtUtc)
        : base(id)
    {
        BatchId = batchId;
        GroupId = groupId;
        Input = input;
        Result = result;
        FormulaCode = formulaCode;
        FormulaExpression = formulaExpression;
        PreparedBy = preparedBy;
        PreparedAtUtc = preparedAtUtc;
    }

    /// <summary>The batch (leva) this preparation was prepared for, referenced by value.</summary>
    public Guid BatchId { get; private set; }

    /// <summary>The dose group (treatment arm) this preparation was prepared for, referenced by value.</summary>
    public Guid GroupId { get; private set; }

    /// <summary>The frozen inputs of the preparation (dose, weights, relation, compound state/density, vehicle flag).</summary>
    public InVivoPreparationInput Input { get; private set; }

    /// <summary>The frozen result (compound mass/volume, final solution volume, diluent volume).</summary>
    public InVivoPreparationResult Result { get; private set; }

    /// <summary>The versioned formula code the result was computed with (e.g. <c>invivo-preparation@v1</c>).</summary>
    public string FormulaCode { get; private set; }

    /// <summary>The human-readable formula expression, for the traceable snapshot.</summary>
    public string FormulaExpression { get; private set; }

    /// <summary>Operator who confirmed the preparation (identity claim).</summary>
    public string PreparedBy { get; private set; }

    /// <summary>Instant (UTC) the preparation was confirmed.</summary>
    public DateTime PreparedAtUtc { get; private set; }

    /// <summary>
    /// Freezes a confirmed preparation from its inputs and the calculator's result, pinning the formula version and
    /// stamping the author + instant. The <paramref name="batchId"/>/<paramref name="groupId"/> are guarded non-empty
    /// here; that they belong to this project is guarded by the aggregate root before this call.
    /// </summary>
    public static SolutionPreparation Create(
        Guid batchId,
        Guid groupId,
        InVivoPreparationInput input,
        InVivoPreparationResult result,
        string preparedBy,
        DateTime preparedAtUtc)
    {
        Guard.AgainstEmptyGuid(batchId, nameof(batchId));
        Guard.AgainstEmptyGuid(groupId, nameof(groupId));
        Guard.AgainstNull(input, nameof(input));
        Guard.AgainstNull(result, nameof(result));

        return new SolutionPreparation(
            Guid.NewGuid(),
            batchId,
            groupId,
            input,
            result,
            Normalize(InVivoPreparationCalculator.FormulaCode, MaxFormulaCodeLength, nameof(InVivoPreparationCalculator.FormulaCode)),
            Normalize(InVivoPreparationCalculator.FormulaExpression, MaxFormulaExpressionLength, nameof(InVivoPreparationCalculator.FormulaExpression)),
            Normalize(preparedBy, MaxPreparedByLength, nameof(preparedBy)),
            preparedAtUtc);
    }

    private static string Normalize(string value, int maxLength, string parameterName)
    {
        Guard.AgainstNullOrWhiteSpace(value, parameterName);
        string trimmed = value.Trim();
        Guard.AgainstMaxLength(trimmed, maxLength, parameterName);
        return trimmed;
    }
}
