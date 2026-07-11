using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// Manufacturer lot/batch identifier used for traceability of a stock item. Optional: items that
/// are not lot-controlled carry no lot, represented by <see langword="null"/> rather than by an
/// empty <see cref="Lot"/> instance. When present, the code is always non-empty.
/// </summary>
public sealed class Lot : ValueObject
{
    private const int MaxLength = 64;

    private Lot(string code) => Code = code;

    public string Code { get; }

    /// <summary>Creates a lot from a non-empty code, or returns <see langword="null"/> for non-lot-controlled items.</summary>
    public static Lot? FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        string trimmed = code.Trim();
        if (trimmed.Length > MaxLength)
            throw new DomainException($"Lot code exceeds the maximum length of {MaxLength} characters.");

        return new Lot(trimmed);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Code;
    }

    public override string ToString() => Code;
}
