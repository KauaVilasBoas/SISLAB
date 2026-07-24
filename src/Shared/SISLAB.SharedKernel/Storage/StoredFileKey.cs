using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Guards;

namespace SISLAB.SharedKernel.Storage;

/// <summary>
/// An opaque, immutable handle to a stored file (SISLAB-09). It is the <i>only</i> thing a domain aggregate keeps about
/// a file: not a path, not a URL, just a backend-agnostic key that an <see cref="IFileStorage"/> adapter minted on save
/// and can resolve on read. Because it is opaque, the same key works whether the bytes live on the local placeholder
/// disk or, later, in an S3 object — the aggregate is unaffected by the swap.
/// </summary>
/// <remarks>
/// Structural equality by value: two keys are equal when their string value is equal, so the key round-trips through
/// persistence as a plain string and compares by content. The value's shape is an adapter concern (the local adapter
/// uses a GUID-derived relative name); callers must treat it as opaque and never parse it.
/// </remarks>
public sealed class StoredFileKey : ValueObject
{
    private StoredFileKey(string value) => Value = value;

    /// <summary>The opaque backend key. Persisted as-is; treated as a black box by callers.</summary>
    public string Value { get; }

    /// <summary>Wraps a validated, non-empty backend key. Used by an adapter after it mints the key on save.</summary>
    public static StoredFileKey Of(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value, nameof(value));
        return new StoredFileKey(value.Trim());
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
