using Microsoft.Extensions.Logging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Infrastructure.Multitenancy;

/// <summary>
/// Scoped implementation of <see cref="ITenantBypass"/>.
///
/// Registered per unit of work (Scoped) so a bypass opened inside a job iteration does not
/// bleed into other scopes. Every scope open/close is logged at Warning level, keeping
/// cross-tenant access auditable as required by the multi-tenancy design.
///
/// Re-entrant: nested <see cref="BeginScope(string)"/> calls are counted, and isolation is
/// only restored once the outermost scope is disposed.
/// </summary>
public sealed class TenantBypass : ITenantBypass
{
    private readonly ILogger<TenantBypass> _logger;
    private int _depth;

    public TenantBypass(ILogger<TenantBypass> logger) => _logger = logger;

    /// <inheritdoc />
    public bool IsActive => _depth > 0;

    /// <inheritdoc />
    public IDisposable BeginScope(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A tenant-bypass scope requires an audit reason.", nameof(reason));

        _depth++;
        _logger.LogWarning(
            "Tenant isolation bypassed (reason: {Reason}, depth: {Depth}).", reason, _depth);

        return new BypassHandle(this, reason);
    }

    private void EndScope(string reason)
    {
        _depth--;
        _logger.LogWarning(
            "Tenant isolation restored (reason: {Reason}, depth: {Depth}).", reason, _depth);
    }

    private sealed class BypassHandle : IDisposable
    {
        private readonly TenantBypass _owner;
        private readonly string _reason;
        private bool _disposed;

        public BypassHandle(TenantBypass owner, string reason)
        {
            _owner = owner;
            _reason = reason;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner.EndScope(_reason);
        }
    }
}
