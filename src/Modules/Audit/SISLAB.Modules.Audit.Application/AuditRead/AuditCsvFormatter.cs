using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SISLAB.Modules.Audit.Application.AuditRead;

/// <summary>
/// Renders audit entries as RFC 4180 CSV for the export endpoint (card [E9] #57). Columns are in
/// Portuguese and contain only information meaningful to a lab user — technical IDs and raw JSON
/// payloads are omitted. The payload is parsed per-action into readable quantity / unit / divergence /
/// reason fields.
/// </summary>
internal static class AuditCsvFormatter
{
    private static readonly string[] Header =
        ["Data/Hora", "Responsável", "Ação", "Tipo", "Quantidade", "Unidade", "Divergência", "Motivo"];

    /// <summary>Formats the entries into a single CSV document (header + one line per entry).</summary>
    public static string ToCsv(IReadOnlyList<AuditEntryListItem> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Header.Select(Escape)));

        foreach (AuditEntryListItem entry in entries)
        {
            PayloadFields fields = ParsePayload(entry.Payload);

            string[] cells =
            [
                entry.OccurredAtUtc.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                entry.UserId,
                TranslateAction(entry.Action),
                TranslateEntityType(entry.EntityType),
                fields.Quantity,
                fields.Unit,
                fields.Divergence,
                fields.Reason,
            ];

            sb.AppendLine(string.Join(',', cells.Select(Escape)));
        }

        return sb.ToString();
    }

    private static string TranslateAction(string action) => action switch
    {
        "consumption"            => "Consumo",
        "disposal"               => "Descarte",
        "stock-count"            => "Conferência",
        "equipment-maintenance"  => "Manutenção de equipamento",
        "equipment-calibration"  => "Calibração de equipamento",
        _                        => action,
    };

    private static string TranslateEntityType(string entityType) => entityType switch
    {
        "StockItem"  => "Item de estoque",
        "Equipment"  => "Equipamento",
        _            => entityType,
    };

    private static PayloadFields ParsePayload(string payload)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(payload);
            JsonElement root = doc.RootElement;

            string quantity = ReadNumber(root, "Quantity", "CountedQuantity");
            string unit     = ReadString(root, "Unit");
            string diverg   = ReadNumber(root, "Divergence");
            string reason   = ReadString(root, "Reason");

            return new(quantity, unit, diverg, reason);
        }
        catch
        {
            return PayloadFields.Empty;
        }
    }

    private static string ReadNumber(JsonElement root, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (root.TryGetProperty(key, out JsonElement el) &&
                el.ValueKind is JsonValueKind.Number)
            {
                return el.ToString();
            }
        }
        return string.Empty;
    }

    private static string ReadString(JsonElement root, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (root.TryGetProperty(key, out JsonElement el) &&
                el.ValueKind is JsonValueKind.String)
            {
                return el.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static string Escape(string value)
    {
        bool mustQuote =
            value.Contains(',') || value.Contains('"') ||
            value.Contains('\n') || value.Contains('\r');

        return mustQuote ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private sealed record PayloadFields(string Quantity, string Unit, string Divergence, string Reason)
    {
        public static readonly PayloadFields Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
