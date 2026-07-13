namespace SISLAB.Modules.Audit.Contracts;

/// <summary>
/// Public port for appending to the audit trail (card [E9] #57). Other modules (Inventory) inject this to
/// record sensitive operations — controlled-item movements and equipment interventions — without depending
/// on the Audit module's internals.
///
/// <para>The write is append-only and independent of the caller's unit of work: the entry is committed
/// on its own so the trail survives even if a later step fails. Implementations write directly via Dapper
/// (no EF change tracking — there is nothing to track on a write-once row).</para>
/// </summary>
public interface IAuditWriter
{
    /// <summary>Appends a single audit entry. Idempotent on the entry's <see cref="AuditEntry.Id"/>.</summary>
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
