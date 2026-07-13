using SISLAB.Modules.Audit.Application.AuditRead;

namespace SISLAB.Modules.Audit.Tests;

/// <summary>
/// Tests for <see cref="AuditCsvFormatter"/> (card [E9] #57): a header row is always emitted, entries are
/// rendered one per line, and RFC 4180 escaping keeps a JSON payload (with commas/quotes) from breaking a row.
/// </summary>
public sealed class AuditCsvFormatterTests
{
    [Fact]
    public void Emits_only_the_header_for_an_empty_set()
    {
        string csv = AuditCsvFormatter.ToCsv([]);

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.StartsWith("Id,UserId,Action,EntityType,EntityId,OccurredAtUtc,Payload", lines[0]);
    }

    [Fact]
    public void Renders_one_line_per_entry()
    {
        IReadOnlyList<AuditEntryListItem> entries =
        [
            Entry(action: "consumption", payload: "{\"q\":30}"),
            Entry(action: "disposal", payload: "{\"q\":5}")
        ];

        string csv = AuditCsvFormatter.ToCsv(entries);

        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 entries
    }

    [Fact]
    public void Escapes_fields_that_contain_commas_or_quotes()
    {
        // A JSON payload naturally contains commas and quotes; it must be quoted and its quotes doubled so it
        // stays in a single CSV cell.
        AuditEntryListItem entry = Entry(action: "consumption", payload: "{\"reason\":\"a, b\"}");

        string csv = AuditCsvFormatter.ToCsv([entry]);

        Assert.Contains("\"{\"\"reason\"\":\"\"a, b\"\"}\"", csv);
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
