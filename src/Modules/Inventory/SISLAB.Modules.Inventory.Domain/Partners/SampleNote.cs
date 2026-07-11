using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.Modules.Inventory.Domain.Partners;

/// <summary>
/// A lightweight, structured note about a sample/compound a <see cref="Partner"/> sent to the lab for
/// testing (for example "GDA-43 · pendente"). It carries a free-text <see cref="Reference"/> and an
/// optional short <see cref="Status"/>.
/// </summary>
/// <remarks>
/// Scope guard (card [E3] #28): this is deliberately a light, descriptive value object on the Partner,
/// <b>not</b> a first-class "test compound" entity. That entity belongs to the Experiments module, which
/// is not part of the current backlog and must not be modelled here. Keeping samples as value objects
/// avoids leaking that future concern into the Inventory aggregate.
/// </remarks>
public sealed class SampleNote : ValueObject
{
    private const int MaxReferenceLength = 120;
    private const int MaxStatusLength = 60;

    private SampleNote(string reference, string? status)
    {
        Reference = reference;
        Status = status;
    }

    /// <summary>Identifier/description of the sample as informed by the partner (e.g. "GDA-92").</summary>
    public string Reference { get; }

    /// <summary>Optional short status of the sample (e.g. "pendente"); <see langword="null"/> when unknown.</summary>
    public string? Status { get; }

    /// <summary>Creates a sample note from a non-empty reference and an optional status.</summary>
    public static SampleNote Create(string reference, string? status = null)
    {
        Guard.AgainstNullOrWhiteSpace(reference, nameof(reference));

        string trimmedReference = reference.Trim();
        if (trimmedReference.Length > MaxReferenceLength)
            throw new DomainException(
                $"Sample reference exceeds the maximum length of {MaxReferenceLength} characters.");

        string? normalizedStatus = NormalizeStatus(status);

        return new SampleNote(trimmedReference, normalizedStatus);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Reference;
        yield return Status;
    }

    public override string ToString() => Status is null ? Reference : $"{Reference} · {Status}";

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        string trimmed = status.Trim();
        if (trimmed.Length > MaxStatusLength)
            throw new DomainException(
                $"Sample status exceeds the maximum length of {MaxStatusLength} characters.");

        return trimmed;
    }
}
