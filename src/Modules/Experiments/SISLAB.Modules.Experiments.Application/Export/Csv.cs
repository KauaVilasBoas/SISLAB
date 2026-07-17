namespace SISLAB.Modules.Experiments.Application.Export;

/// <summary>Minimal RFC 4180 field escaping shared by the Prism CSV formatters.</summary>
internal static class Csv
{
    /// <summary>
    /// Quotes a field when it contains a comma, quote or newline, doubling any embedded quotes. Kept tiny and
    /// dependency-free — the formatters emit small, well-known tables, not arbitrary user text.
    /// </summary>
    public static string Escape(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
