using Microsoft.Extensions.DependencyInjection;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;
using SISLAB.SharedKernel.Time;

namespace SISLAB.Jobs.Tests.Jobs.TestSupport;

/// <summary>
/// In-memory host that drives a real <see cref="Jobs.CompanyScanAlertJob"/> through one tick without a
/// database. It reproduces the production shape faithfully: a fresh child scope per company, each with its
/// own real <see cref="TenantContextOverride"/>, and an <see cref="IMediator"/> that answers the E4/E6 read
/// query using ONLY the company set on that scope's override — exactly as the real Dapper handlers read the
/// effective <c>ITenantContext</c>. This is what lets the tests prove cross-tenant isolation: a company only
/// ever sees its own rows.
/// </summary>
/// <typeparam name="TRow">The at-risk row type the mediator returns per company (ExpiringItem, etc.).</typeparam>
internal sealed class AlertJobTestHost<TRow> : IServiceScopeFactory
{
    private readonly IReadOnlyList<Guid> _companyIds;
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<TRow>> _rowsByCompany;
    private readonly ISet<Guid> _failingCompanies;

    public AlertJobTestHost(
        IReadOnlyList<Guid> companyIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<TRow>> rowsByCompany,
        IEnumerable<Guid>? failingCompanies = null)
    {
        _companyIds = companyIds;
        _rowsByCompany = rowsByCompany;
        _failingCompanies = new HashSet<Guid>(failingCompanies ?? []);
    }

    /// <summary>The shared clock every job resolves; fixed so dedupe-key days are deterministic.</summary>
    public FixedClock Clock { get; } = new(new DateTime(2026, 7, 13, 0, 0, 0, DateTimeKind.Utc));

    /// <summary>Records every notification raised, tagged with the override company active when it was raised.</summary>
    public RecordingNotificationPublisher Publisher { get; } = new();

    /// <summary>Counts how many bypass scopes the job opened (auditability).</summary>
    public int BypassScopesOpened => _bypass.ScopesOpened;

    private readonly RecordingTenantBypass _bypass = new();

    public IServiceScope CreateScope() => new Scope(this);

    private sealed class Scope : IServiceScope, IServiceProvider
    {
        private readonly AlertJobTestHost<TRow> _host;
        private readonly TenantContextOverride _tenantOverride = new();

        public Scope(AlertJobTestHost<TRow> host) => _host = host;

        public IServiceProvider ServiceProvider => this;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ITenantContextOverride)) return _tenantOverride;
            if (serviceType == typeof(ITenantBypass)) return _host._bypass;
            if (serviceType == typeof(IClock)) return _host.Clock;
            if (serviceType == typeof(Modules.Notifications.Contracts.INotificationPublisher)) return _host.Publisher;
            if (serviceType == typeof(IMediator))
                return new ScopedMediator(_host, _tenantOverride);
            return null;
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Mediator that answers ListAllCompanyIdsQuery with the configured ids, and any other paged query with
    /// the rows of the company currently set on THIS scope's override — throwing for a company flagged as
    /// failing (to exercise resilience). Reading the override, not a query argument, is the whole point: it
    /// mirrors the real handlers and makes tenant leakage impossible to fake away.
    /// </summary>
    private sealed class ScopedMediator : IMediator
    {
        private readonly AlertJobTestHost<TRow> _host;
        private readonly ITenantContextOverride _tenantOverride;

        public ScopedMediator(AlertJobTestHost<TRow> host, ITenantContextOverride tenantOverride)
        {
            _host = host;
            _tenantOverride = tenantOverride;
        }

        public Task<TResult> SendAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
        {
            if (request is ListAllCompanyIdsQuery)
            {
                object result = new ListAllCompanyIdsQueryResult(_host._companyIds);
                return Task.FromResult((TResult)result);
            }

            if (request is PagedQuery<PagedResult<TRow>>)
            {
                Guid company = _tenantOverride.CompanyId
                    ?? throw new InvalidOperationException(
                        "The read query ran without an active tenant override — the job failed to scope it.");

                if (_host._failingCompanies.Contains(company))
                    throw new InvalidOperationException($"Simulated read failure for company {company}.");

                IReadOnlyList<TRow> rows = _host._rowsByCompany.TryGetValue(company, out IReadOnlyList<TRow>? r)
                    ? r
                    : [];

                var page = new PagedResult<TRow>(rows, rows.Count, page: 1, pageSize: 200);
                return Task.FromResult((TResult)(object)page);
            }

            throw new InvalidOperationException($"Unexpected request type {request.GetType().Name}.");
        }
    }

    private sealed class RecordingTenantBypass : ITenantBypass
    {
        private int _depth;
        public int ScopesOpened { get; private set; }
        public bool IsActive => _depth > 0;

        public IDisposable BeginScope(string reason)
        {
            ScopesOpened++;
            _depth++;
            return new Handle(this);
        }

        private sealed class Handle : IDisposable
        {
            private readonly RecordingTenantBypass _owner;
            private bool _disposed;
            public Handle(RecordingTenantBypass owner) => _owner = owner;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _owner._depth--;
            }
        }
    }
}

/// <summary>Fixed clock so day-bucketed dedupe keys are deterministic in tests.</summary>
internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;
    public DateTime UtcNow { get; }
}

/// <summary>
/// Notification publisher double that records each raised request alongside the tenant-override company that
/// was active when it was raised. Its idempotency is faithful to the real publisher's dedupe-by-key contract:
/// the same (dedupe key) is a no-op after the first raise, so the tests can assert per-cycle de-duplication.
/// </summary>
internal sealed class RecordingNotificationPublisher : Modules.Notifications.Contracts.INotificationPublisher
{
    private readonly HashSet<string> _seenKeys = [];

    public List<Modules.Notifications.Contracts.RaiseNotificationRequest> Raised { get; } = [];

    public Task<bool> RaiseAsync(
        Modules.Notifications.Contracts.RaiseNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        bool isNew = _seenKeys.Add(request.DedupeKey);
        if (isNew)
            Raised.Add(request);

        return Task.FromResult(isNew);
    }
}
