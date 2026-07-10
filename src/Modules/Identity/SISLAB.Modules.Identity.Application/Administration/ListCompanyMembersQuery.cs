using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>Lists the members of a company (the active tenant).</summary>
/// <param name="CompanyId">Active company resolved from <c>ITenantContext</c>, never from the client.</param>
public sealed record ListCompanyMembersQuery(Guid CompanyId)
    : IQuery<ListCompanyMembersResult>;

/// <param name="CompanyExists">
/// False when no company matches the id (distinguishes 404 from an empty member list).
/// </param>
public sealed record ListCompanyMembersResult(
    bool CompanyExists,
    IReadOnlyList<CompanyMemberDto> Members)
{
    public static ListCompanyMembersResult NotFound() => new(false, []);
}

internal sealed class ListCompanyMembersQueryHandler
    : IQueryHandler<ListCompanyMembersQuery, ListCompanyMembersResult>
{
    private readonly ICompanyRepository _companyRepository;

    public ListCompanyMembersQueryHandler(ICompanyRepository companyRepository)
        => _companyRepository = companyRepository;

    public async Task<ListCompanyMembersResult> HandleAsync(
        ListCompanyMembersQuery request,
        CancellationToken cancellationToken = default)
    {
        Company? company = await _companyRepository.FindByIdAsync(request.CompanyId, cancellationToken);
        if (company is null)
            return ListCompanyMembersResult.NotFound();

        var members = company.Memberships
            .Select(m => new CompanyMemberDto(m.Id, m.LumenUserId))
            .ToList();

        return new ListCompanyMembersResult(true, members);
    }
}
