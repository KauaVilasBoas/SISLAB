using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>
/// Lists the ids of every <b>active</b> company (tenant) in the system (E6, Fork #2 → E1). Unlike the other
/// Identity queries this one is <b>not</b> scoped to a single tenant: it is the cross-tenant enumeration the
/// background alert jobs (#41/#42/#66) use to iterate every company. The jobs resolve <c>IMediator</c> under
/// an auditable <c>ITenantBypass</c> scope, send this query to obtain the id list, and then run each E6 read
/// query once per company (setting the tenant override per company).
/// </summary>
/// <remarks>
/// It intentionally returns only the ids, not the <c>Company</c> aggregates: the jobs need nothing but the
/// tenant identifier to drive the per-company scan, and returning ids keeps the contract minimal and the
/// payload flat. "Active" mirrors <see cref="ICompanyRepository.ListActiveAsync"/> — a deactivated company
/// is excluded, so no alerts are raised for tenants that are no longer operating.
/// </remarks>
public sealed record ListAllCompanyIdsQuery : IQuery<ListAllCompanyIdsQueryResult>;

/// <param name="CompanyIds">Ids of the active companies (never null; empty when there are none).</param>
public sealed record ListAllCompanyIdsQueryResult(IReadOnlyList<Guid> CompanyIds);

internal sealed class ListAllCompanyIdsQueryHandler
    : IQueryHandler<ListAllCompanyIdsQuery, ListAllCompanyIdsQueryResult>
{
    private readonly ICompanyRepository _companyRepository;

    public ListAllCompanyIdsQueryHandler(ICompanyRepository companyRepository)
        => _companyRepository = companyRepository;

    public async Task<ListAllCompanyIdsQueryResult> HandleAsync(
        ListAllCompanyIdsQuery request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Company> companies = await _companyRepository.ListActiveAsync(cancellationToken);

        IReadOnlyList<Guid> ids = companies
            .Select(company => company.Id)
            .ToList();

        return new ListAllCompanyIdsQueryResult(ids);
    }
}
