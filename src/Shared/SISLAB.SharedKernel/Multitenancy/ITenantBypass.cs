namespace SISLAB.SharedKernel.Multitenancy;

/// <summary>
/// Explicit, auditable escape hatch from tenant isolation for system/background work
/// (e.g. SISLAB.Jobs processing the Outbox, cross-tenant alert scans) that legitimately
/// needs to read/write across companies without an active HTTP request.
///
/// The bypass is <b>never</b> implicit: a caller must open a scope on purpose, and the scope
/// is scoped to a <c>using</c> block so it cannot silently leak into unrelated operations.
/// While a bypass scope is open, <see cref="IsActive"/> is <c>true</c> and the EF Core global
/// query filter is disabled for the current unit of work.
/// </summary>
public interface ITenantBypass
{
    /// <summary>
    /// <c>true</c> while a bypass scope opened by <see cref="BeginScope(string)"/> is in effect.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Opens an auditable tenant-bypass scope. The <paramref name="reason"/> is required and
    /// is logged, so every cross-tenant access is traceable. Disposing the returned handle
    /// restores tenant isolation.
    /// </summary>
    /// <param name="reason">Human-readable justification (logged) — e.g. "outbox-dispatch".</param>
    IDisposable BeginScope(string reason);
}
