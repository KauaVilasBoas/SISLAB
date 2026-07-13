using SISLAB.Modules.Audit.Contracts;

namespace SISLAB.Modules.Inventory.Tests.Application.Audit;

/// <summary>
/// Test double for <see cref="IAuditWriter"/> that captures every appended <see cref="AuditEntry"/>, so
/// handler tests can assert whether — and with what payload — a sensitive operation was recorded on the
/// audit trail (card [E9] #57).
/// </summary>
internal sealed class CapturingAuditWriter : IAuditWriter
{
    public List<AuditEntry> Entries { get; } = new();

    public AuditEntry? LastEntry => Entries.Count > 0 ? Entries[^1] : null;

    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>Fixed <see cref="IAuditActorAccessor"/> returning a known actor id for deterministic tests.</summary>
internal sealed class StubAuditActorAccessor : IAuditActorAccessor
{
    private readonly string _actor;

    public StubAuditActorAccessor(string actor = "user-42") => _actor = actor;

    public string GetCurrentActor() => _actor;
}
