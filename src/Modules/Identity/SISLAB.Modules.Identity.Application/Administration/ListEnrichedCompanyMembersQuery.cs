using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>
/// Lists the members of the active company enriched with their Lumen identity (username/e-mail) and assigned
/// profiles (card [E7] #105), so the "Members" tab can render name, e-mail and profile chips in one request.
///
/// <para>Joins the two stores server-side: SISLAB's tenancy store owns the membership links (guarded by
/// <c>company_id</c>), Lumen owns the identity and profile assignments. Membership is the source of truth for
/// <i>who</i> belongs to the company; enrichment is a best-effort read — a membership whose Lumen account no
/// longer exists is skipped rather than failing the whole listing.</para>
/// </summary>
/// <param name="CompanyId">Active company resolved from <c>ITenantContext</c>, never from the client.</param>
public sealed record ListEnrichedCompanyMembersQuery(Guid CompanyId)
    : IQuery<ListEnrichedCompanyMembersQueryResult>;

/// <param name="Members">Enriched members of the active company (never null; empty when it has none).</param>
public sealed record ListEnrichedCompanyMembersQueryResult(IReadOnlyList<EnrichedMemberDto> Members);

internal sealed class ListEnrichedCompanyMembersQueryHandler
    : IQueryHandler<ListEnrichedCompanyMembersQuery, ListEnrichedCompanyMembersQueryResult>
{
    private readonly ICompanyRepository _companyRepository;
    private readonly ILumenUserGateway _userGateway;

    public ListEnrichedCompanyMembersQueryHandler(
        ICompanyRepository companyRepository,
        ILumenUserGateway userGateway)
    {
        _companyRepository = companyRepository;
        _userGateway = userGateway;
    }

    public async Task<ListEnrichedCompanyMembersQueryResult> HandleAsync(
        ListEnrichedCompanyMembersQuery request,
        CancellationToken cancellationToken = default)
    {
        Company company = await _companyRepository.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        var members = new List<EnrichedMemberDto>(company.Memberships.Count);
        foreach (CompanyMembership membership in company.Memberships)
        {
            MemberEnrichmentDto? enrichment =
                await _userGateway.EnrichMemberAsync(membership.LumenUserId, cancellationToken);

            if (enrichment is null)
                continue;

            members.Add(new EnrichedMemberDto(
                membership.Id,
                membership.LumenUserId,
                enrichment.Username,
                enrichment.Email,
                enrichment.AssignedProfiles));
        }

        return new ListEnrichedCompanyMembersQueryResult(members);
    }
}
