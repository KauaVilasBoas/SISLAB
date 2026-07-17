using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Experiments;

/// <summary>
/// Immutable, versioned record of a calculation that was applied to an experiment (decision card #68 —
/// "fórmulas versionadas" as the antidote to the Excel <c>#ERROR!</c>). It captures <b>which</b> formula ran
/// (<see cref="FormulaName"/>, a <c>name@version</c> code such as <c>viability@v1</c>), its human-readable
/// <see cref="FormulaExpression"/>, <b>when</b> it ran (<see cref="AppliedAtUtc"/>) and the <b>frozen</b>
/// result (<see cref="ResultJson"/>).
/// </summary>
/// <remarks>
/// The snapshot is the reproducibility guarantee: the result is stored as computed, never recomputed on read,
/// so a later change to a strategy can never silently rewrite a signed-off experiment's numbers. It is a value
/// object (structural equality by its four components) owned by the experiment aggregate. The result payload is
/// kept as an opaque JSON string here — the domain does not interpret it; the read-side query shapes it for the
/// UI, and the strategy that produced it owns its schema.
/// </remarks>
public sealed class FormulaSnapshot : ValueObject
{
    private const int MaxFormulaNameLength = 100;
    private const int MaxFormulaExpressionLength = 1000;

    // Parameterless constructor for EF Core materialization of the owned value object.
    private FormulaSnapshot()
    {
        FormulaName = default!;
        FormulaExpression = default!;
        ResultJson = default!;
    }

    private FormulaSnapshot(string formulaName, string formulaExpression, DateTime appliedAtUtc, string resultJson)
    {
        FormulaName = formulaName;
        FormulaExpression = formulaExpression;
        AppliedAtUtc = appliedAtUtc;
        ResultJson = resultJson;
    }

    /// <summary>Versioned formula code, e.g. <c>viability@v1</c> — identifies exactly which formula ran.</summary>
    public string FormulaName { get; }

    /// <summary>Human-readable expression of the formula, for audit/traceability.</summary>
    public string FormulaExpression { get; }

    /// <summary>Instant (UTC) the formula was applied.</summary>
    public DateTime AppliedAtUtc { get; }

    /// <summary>Frozen result payload as a JSON string — the snapshot never recomputed on read.</summary>
    public string ResultJson { get; }

    /// <summary>Creates a snapshot, guarding the code/expression/payload against empty values.</summary>
    public static FormulaSnapshot Create(
        string formulaName,
        string formulaExpression,
        DateTime appliedAtUtc,
        string resultJson)
    {
        Guard.AgainstNullOrWhiteSpace(formulaName, nameof(formulaName));
        string trimmedName = formulaName.Trim();
        Guard.AgainstMaxLength(trimmedName, MaxFormulaNameLength, nameof(formulaName));

        Guard.AgainstNullOrWhiteSpace(formulaExpression, nameof(formulaExpression));
        string trimmedExpression = formulaExpression.Trim();
        Guard.AgainstMaxLength(trimmedExpression, MaxFormulaExpressionLength, nameof(formulaExpression));

        Guard.AgainstNullOrWhiteSpace(resultJson, nameof(resultJson));

        return new FormulaSnapshot(trimmedName, trimmedExpression, appliedAtUtc, resultJson);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FormulaName;
        yield return FormulaExpression;
        yield return AppliedAtUtc;
        yield return ResultJson;
    }
}
