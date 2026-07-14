using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;
using SISLAB.Modules.Configuration.Domain.ExpiryPolicies;
using SISLAB.SharedKernel.Messaging;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Configuration.Application.ExpiryPolicies;

/// <summary>
/// Read-side query (card [E12] #76) that returns the active company's expiry warning window in days. It reads
/// <c>configuration.expiry_policies</c> via Dapper — never the write DbContext — and, when the tenant has no
/// policy configured yet, falls back to <see cref="ExpiryPolicy.DefaultWarningWindowDays"/> so callers always
/// get a usable value. Backs both the config screen and the <c>ILabConfiguration</c> port the Inventory
/// read-side consumes to classify expiry.
/// </summary>
/// <remarks>
/// <b>Tenant scoping.</b> The company is taken from <see cref="ITenantContext"/> by the handler, never from
/// the request, and the SELECT keeps <c>WHERE company_id = @CompanyId</c> — the read-side has no EF global
/// query filter, so the tenant guard is explicit (defense-in-depth, section 7).
/// </remarks>
public sealed record GetExpiryWarningWindowQuery : IQuery<int>;

internal sealed class GetExpiryWarningWindowQueryHandler
    : BaseDataAccess, IQueryHandler<GetExpiryWarningWindowQuery, int>
{
    private const string Sql =
        """
        SELECT p.warning_window_days
        FROM configuration.expiry_policies AS p
        WHERE p.company_id = @CompanyId
        LIMIT 1;
        """;

    private readonly ITenantContext _tenantContext;

    public GetExpiryWarningWindowQueryHandler(
        DbConnectionFactory connectionFactory,
        ITenantContext tenantContext)
        : base(connectionFactory)
        => _tenantContext = tenantContext;

    public async Task<int> HandleAsync(
        GetExpiryWarningWindowQuery request,
        CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await OpenConnectionAsync();

        int? configured = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                Sql,
                new { _tenantContext.CompanyId },
                cancellationToken: cancellationToken));

        // No policy configured yet → the sensible default, so the Inventory read-side always has a window.
        return configured ?? ExpiryPolicy.DefaultWarningWindowDays;
    }
}
