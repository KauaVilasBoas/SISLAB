using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>Lists the members of a company (the active tenant).</summary>
/// <param name="CompanyId">Active company resolved from <c>ITenantContext</c>, never from the client.</param>
public sealed record ListCompanyMembersQuery(Guid CompanyId)
    : IQuery<ListCompanyMembersQueryResult>;

/// <param name="Members">Flattened members of the active company (never null; empty when it has none).</param>
public sealed record ListCompanyMembersQueryResult(IReadOnlyList<CompanyMemberDto> Members);

internal sealed class ListCompanyMembersQueryHandler
    : IQueryHandler<ListCompanyMembersQuery, ListCompanyMembersQueryResult>
{
    private readonly ICompanyRepository _companyRepository;

    public ListCompanyMembersQueryHandler(ICompanyRepository companyRepository)
        => _companyRepository = companyRepository;

    public async Task<ListCompanyMembersQueryResult> HandleAsync(
        ListCompanyMembersQuery request,
        CancellationToken cancellationToken = default)
    {
        Company company = await _companyRepository.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        var members = company.Memberships
            .Select(m => new CompanyMemberDto(m.Id, m.LumenUserId, m.Role))
            .ToList();

        return new ListCompanyMembersQueryResult(members);
    }
}
