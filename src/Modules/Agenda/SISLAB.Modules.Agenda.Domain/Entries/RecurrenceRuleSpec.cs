using System.Text.RegularExpressions;
using SISLAB.SharedKernel.Domain;

namespace SISLAB.Modules.Agenda.Domain.Entries;

/// <summary>
/// Immutable value object wrapping an RFC 5545 recurrence rule (the <c>RRULE</c> line, without the
/// <c>RRULE:</c> prefix — e.g. <c>FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20260930T000000Z</c>). It exists so a
/// recurrence rule is validated once, at the domain boundary, and travels the write-side as a typed,
/// equality-comparable value rather than a raw <see cref="string"/> (guards against primitive obsession).
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope of validation.</b> The domain does not re-implement a full RFC 5545 parser (the read-side uses
/// Ical.Net for the heavy lifting of occurrence expansion). It enforces only the cheap, structural
/// invariants that keep a persisted rule sane: a non-empty value, a mandatory <c>FREQ</c> part with a
/// supported frequency, and a <c>key=value;…</c> shape. A rule that passes here is well-formed enough for
/// Ical.Net to expand; anything Ical.Net still rejects surfaces as a read-side error, never a corrupt write.
/// </para>
/// </remarks>
public sealed partial class RecurrenceRuleSpec : ValueObject
{
    private static readonly string[] SupportedFrequencies =
        ["SECONDLY", "MINUTELY", "HOURLY", "DAILY", "WEEKLY", "MONTHLY", "YEARLY"];

    /// <summary>The canonical rule text (upper-cased, trimmed), e.g. <c>FREQ=WEEKLY;BYDAY=MO,WE</c>.</summary>
    public string Value { get; }

    private RecurrenceRuleSpec(string value) => Value = value;

    /// <summary>
    /// Parses and validates <paramref name="rrule"/> into a <see cref="RecurrenceRuleSpec"/>. Trims a leading
    /// <c>RRULE:</c> if the caller passed the whole content line. Throws <see cref="ArgumentException"/> when
    /// the rule is empty, malformed (not <c>key=value;…</c>) or declares no supported <c>FREQ</c>.
    /// </summary>
    public static RecurrenceRuleSpec Create(string rrule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rrule);

        string normalized = rrule.Trim().ToUpperInvariant();
        if (normalized.StartsWith("RRULE:", StringComparison.Ordinal))
            normalized = normalized["RRULE:".Length..];

        if (!RulePartsRegex().IsMatch(normalized))
            throw new ArgumentException(
                $"Recurrence rule '{rrule}' is not a well-formed RRULE (expected 'FREQ=…;KEY=VALUE;…').",
                nameof(rrule));

        string? frequency = ExtractPart(normalized, "FREQ");
        if (frequency is null || !SupportedFrequencies.Contains(frequency))
            throw new ArgumentException(
                $"Recurrence rule '{rrule}' must declare a supported FREQ ({string.Join(", ", SupportedFrequencies)}).",
                nameof(rrule));

        return new RecurrenceRuleSpec(normalized);
    }

    /// <summary>
    /// Convenience factory that returns <see langword="null"/> for a null/blank input and otherwise delegates
    /// to <see cref="Create"/>. Lets callers map an optional recurrence field without a null-check dance.
    /// </summary>
    public static RecurrenceRuleSpec? CreateOptional(string? rrule)
        => string.IsNullOrWhiteSpace(rrule) ? null : Create(rrule);

    /// <summary>
    /// Returns a copy of this rule truncated to end on <paramref name="untilUtc"/> by replacing (or adding) the
    /// <c>UNTIL</c> part. Used by the "this and following" edit to close the original series at the split date.
    /// </summary>
    public RecurrenceRuleSpec WithUntil(DateTime untilUtc)
    {
        string until = untilUtc.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'");

        IEnumerable<string> parts = Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !part.StartsWith("UNTIL=", StringComparison.Ordinal)
                        && !part.StartsWith("COUNT=", StringComparison.Ordinal));

        return new RecurrenceRuleSpec(string.Join(';', parts.Append($"UNTIL={until}")));
    }

    private static string? ExtractPart(string rule, string key)
    {
        foreach (string part in rule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = part.IndexOf('=');
            if (eq > 0 && part[..eq] == key)
                return part[(eq + 1)..];
        }

        return null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(@"^[A-Z]+=[^;]+(;[A-Z]+=[^;]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex RulePartsRegex();
}
