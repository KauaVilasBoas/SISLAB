using SISLAB.Modules.Audit.Application.AuditRead;

namespace SISLAB.Modules.Audit.Tests;

/// <summary>
/// Tests for <see cref="AuditCsvFormatter"/> (card [E9] #57). The export is written for a lab user: the header
/// row and cells are Portuguese, the action/entity codes are translated, and the raw JSON payload is parsed into
/// readable quantity / unit / divergence / reason columns. RFC 4180 escaping keeps a value with commas/quotes in
/// a single cell.
/// </summary>
public sealed class AuditCsvFormatterTests
{
    [Fact]
    public void Emits_only_the_header_for_an_empty_set()
    {
        string csv = AuditCsvFormatter.ToCsv([]);

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal("Data/Hora,Responsável,Ação,Tipo,Quantidade,Unidade,Divergência,Motivo", lines[0]);
    }

    [Fact]
    public void Renders_one_line_per_entry()
    {
        IReadOnlyList<AuditEntryListItem> entries =
        [
            Entry(action: "consumption", payload: "{\"Quantity\":30}"),
            Entry(action: "disposal", payload: "{\"Quantity\":5}")
        ];

        string csv = AuditCsvFormatter.ToCsv(entries);

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 entries
    }

    [Fact]
    public void Translates_action_and_parses_the_payload_into_readable_columns()
    {
        AuditEntryListItem entry = Entry(
            action: "consumption",
            payload: "{\"Quantity\":30,\"Unit\":\"mL\",\"Reason\":\"rotina\"}");

        string csv = AuditCsvFormatter.ToCsv([entry]);

        // The action code is translated and the payload is flattened into its own cells — no raw JSON leaks.
        Assert.Contains("Consumo,Item de estoque,30,mL,,rotina", csv);
        Assert.DoesNotContain("Quantity", csv);
    }

    [Fact]
    public void Escapes_fields_that_contain_commas_or_quotes()
    {
        // A reason naturally contains a comma; it must be quoted so it stays in a single CSV cell.
        AuditEntryListItem entry = Entry(
            action: "consumption",
            payload: "{\"Reason\":\"a, b\"}");

        string csv = AuditCsvFormatter.ToCsv([entry]);

        Assert.Contains("\"a, b\"", csv);
    }

    private static AuditEntryListItem Entry(string action, string payload) => new(
        Id: Guid.NewGuid(),
        UserId: "user-42",
        Action: action,
        EntityType: "StockItem",
        EntityId: Guid.NewGuid(),
        Payload: payload,
        OccurredAtUtc: new DateTime(2026, 7, 13, 10, 0, 0, DateTimeKind.Utc));
}
