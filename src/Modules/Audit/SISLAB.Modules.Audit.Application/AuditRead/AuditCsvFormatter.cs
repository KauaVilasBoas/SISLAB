using System.Globalization;
using System.Text;

namespace SISLAB.Modules.Audit.Application.AuditRead;

/// <summary>
/// Renders audit entries as RFC 4180 CSV for the export endpoint (card [E9] #57). The CSV is small and
/// stable, so a hand-rolled writer is used instead of a library dependency — it still escapes quotes,
/// commas and newlines correctly so a payload JSON never breaks a row.
/// </summary>
internal static class AuditCsvFormatter
{
    private static readonly string[] Header =
        ["Id", "UserId", "Action", "EntityType", "EntityId", "OccurredAtUtc", "Payload"];

    /// <summary>Formats the entries into a single CSV document (header + one line per entry).</summary>
    public static string ToCsv(IReadOnlyList<AuditEntryListItem> entries)
    {
        var builder = new StringBuilder();

        builder.AppendLine(string.Join(',', Header.Select(Escape)));

        foreach (AuditEntryListItem entry in entries)
        {
            string[] cells =
            [
                entry.Id.ToString(),
                entry.UserId,
                entry.Action,
                entry.EntityType,
                entry.EntityId.ToString(),
                entry.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                entry.Payload
            ];

            builder.AppendLine(string.Join(',', cells.Select(Escape)));
        }

        return builder.ToString();
    }

    /// <summary>Quotes a field when it contains a comma, quote or line break; doubles embedded quotes.</summary>
    private static string Escape(string value)
    {
        bool mustQuote =
            value.Contains(',') || value.Contains('"') ||
            value.Contains('\n') || value.Contains('\r');

        if (!mustQuote)
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
