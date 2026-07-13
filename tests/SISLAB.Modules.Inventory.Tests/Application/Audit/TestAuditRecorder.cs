using SISLAB.Modules.Inventory.Application.Audit;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Inventory.Tests.Application.Audit;

/// <summary>
/// Builds an <see cref="InventoryAuditRecorder"/> wired to test doubles, exposing the capturing writer so a
/// handler test can both run the handler and inspect what (if anything) landed on the audit trail.
/// </summary>
internal sealed class TestAuditRecorder
{
    private TestAuditRecorder(InventoryAuditRecorder recorder, CapturingAuditWriter writer)
    {
        Recorder = recorder;
        Writer = writer;
    }

    /// <summary>The recorder under test, ready to inject into a command handler.</summary>
    public InventoryAuditRecorder Recorder { get; }

    /// <summary>The capturing writer behind the recorder — assert against <see cref="CapturingAuditWriter.Entries"/>.</summary>
    public CapturingAuditWriter Writer { get; }

    public static TestAuditRecorder Create(string actor = "user-42")
    {
        var writer = new CapturingAuditWriter();
        var recorder = new InventoryAuditRecorder(
            writer,
            new StubAuditActorAccessor(actor),
            FixedClock.On(2026, 7, 13));

        return new TestAuditRecorder(recorder, writer);
    }
}
