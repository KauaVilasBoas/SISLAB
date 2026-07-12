using System.Text.RegularExpressions;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Inventory.Domain.ValueObjects;

/// <summary>
/// Institutional contact e-mail of a <see cref="StockItems.StockItem"/>'s supplier/partner (for example
/// <c>vendas.br@merck.com</c> or <c>lab.barbosa@ufba.br</c>). Optional on a partner: a partner without a
/// known contact carries <see langword="null"/> rather than an empty <see cref="Email"/> instance. When
/// present, the address is normalized to lower-case and validated against a conservative shape.
/// </summary>
/// <remarks>
/// The SharedKernel had no reusable Email value object at the time this card was implemented (card [E3]
/// #28); it lives here in the Inventory domain, next to the other value objects, until a second bounded
/// context needs it and it is promoted to the SharedKernel. The pattern deliberately mirrors
/// <see cref="Lot"/>: a private constructor plus a <c>FromValue</c> factory that returns
/// <see langword="null"/> for a blank input.
/// </remarks>
public sealed partial class Email : ValueObject
{
    private const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Creates an e-mail from a raw address, or returns <see langword="null"/> when no contact is given.
    /// Fails when a non-blank value is not a syntactically valid address or exceeds the maximum length.
    /// </summary>
    public static Email? FromValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
            throw new DomainException($"E-mail exceeds the maximum length of {MaxLength} characters.");

        if (!AddressPattern().IsMatch(normalized))
            throw new DomainException($"'{value}' is not a valid e-mail address.");

        return new Email(normalized);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    // Conservative address shape: a non-empty local part, an '@', a domain with at least one dot and a
    // 2+ char TLD. Intentionally strict-enough for data hygiene, not a full RFC 5322 parser.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}
