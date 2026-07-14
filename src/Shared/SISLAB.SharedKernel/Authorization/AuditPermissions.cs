namespace SISLAB.SharedKernel.Authorization;

/// <summary>
/// Permission-code catalogue for the Audit bounded context (append-only compliance trail).
/// See <see cref="InventoryPermissions"/> for the <c>&lt;Controller&gt;.&lt;Action&gt;</c> convention.
///
/// <para>Audit exposes only <b>read</b> actions (the trail is written internally, never over HTTP), so
/// there are no write permissions to gate. The read codes are catalogued for completeness and so a
/// future decision to permission-gate reading the compliance trail has a single source of truth.</para>
/// </summary>
public static class AuditPermissions
{
    /// <summary>Read the compliance trail (GET <c>AuditController.List</c>).</summary>
    public const string List = "Audit.List";

    /// <summary>Export the compliance trail (GET <c>AuditController.Export</c>).</summary>
    public const string Export = "Audit.Export";

    /// <summary>
    /// Audit write permissions — intentionally empty: the trail is append-only and written only by
    /// internal module code, never through a decorated write endpoint.
    /// </summary>
    public static IReadOnlySet<string> All { get; } = new HashSet<string>();
}
