using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Experiments.Domain.Projects;

/// <summary>
/// The outcome of applying an inclusion criterion to an animal (SISLAB-02): whether it was
/// <see cref="AnimalInclusionStatus.Included"/> or <see cref="AnimalInclusionStatus.Excluded"/>, on which
/// <see cref="ParameterCode"/>, the <see cref="DecidingValue"/> that motivated the decision and a human-readable
/// <see cref="Reason"/>. An immutable value object with structural equality — the record of a decision, not a mutable
/// flag.
/// </summary>
/// <remarks>
/// Recording the deciding value and reason (not just a boolean) is the point of the selection: the operator needs to
/// see "excluded — glicemia 214 mg/dL &lt; 250" to trust and audit the cut. The value object is rebuilt wholesale on
/// every re-application, so an animal never carries a half-updated decision.
/// </remarks>
public sealed class InclusionDecision : ValueObject
{
    private const int MaxParameterCodeLength = 60;
    private const int MaxReasonLength = 300;

    private InclusionDecision(
        AnimalInclusionStatus status,
        string parameterCode,
        decimal decidingValue,
        string reason)
    {
        Status = status;
        ParameterCode = parameterCode;
        DecidingValue = decidingValue;
        Reason = reason;
    }

    /// <summary>Whether the animal was included or excluded by the criterion.</summary>
    public AnimalInclusionStatus Status { get; }

    /// <summary>The parameter the decision was taken on (e.g. "glicemia").</summary>
    public string ParameterCode { get; }

    /// <summary>The measured value that motivated the decision (the reading tested against the threshold).</summary>
    public decimal DecidingValue { get; }

    /// <summary>A human-readable justification (e.g. "glicemia 268 ≥ 250 mg/dL").</summary>
    public string Reason { get; }

    /// <summary>Records an inclusion (the animal qualified).</summary>
    public static InclusionDecision Included(string parameterCode, decimal decidingValue, string reason)
        => Create(AnimalInclusionStatus.Included, parameterCode, decidingValue, reason);

    /// <summary>Records an exclusion (the animal did not qualify).</summary>
    public static InclusionDecision Excluded(string parameterCode, decimal decidingValue, string reason)
        => Create(AnimalInclusionStatus.Excluded, parameterCode, decidingValue, reason);

    private static InclusionDecision Create(
        AnimalInclusionStatus status,
        string parameterCode,
        decimal decidingValue,
        string reason)
    {
        Guard.AgainstNullOrWhiteSpace(parameterCode, nameof(parameterCode));
        string trimmedParameter = parameterCode.Trim();
        Guard.AgainstMaxLength(trimmedParameter, MaxParameterCodeLength, nameof(parameterCode));

        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));
        string trimmedReason = reason.Trim();
        Guard.AgainstMaxLength(trimmedReason, MaxReasonLength, nameof(reason));

        return new InclusionDecision(status, trimmedParameter, decidingValue, trimmedReason);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        yield return ParameterCode.ToLowerInvariant();
        yield return DecidingValue;
        yield return Reason;
    }
}
