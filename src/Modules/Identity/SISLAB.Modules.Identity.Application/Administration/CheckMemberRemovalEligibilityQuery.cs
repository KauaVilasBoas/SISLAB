using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>Dry-runs removal eligibility for a member of the active company.</summary>
/// <param name="CompanyId">Active company resolved from <c>ITenantContext</c>, never from the client.</param>
/// <param name="UserId">Lumen user to evaluate.</param>
public sealed record CheckMemberRemovalEligibilityQuery(Guid CompanyId, Guid UserId)
    : IQuery<CheckMemberRemovalEligibilityQueryResult>;

/// <param name="Eligibility">Flattened eligibility outcome for the evaluated user.</param>
public sealed record CheckMemberRemovalEligibilityQueryResult(MemberRemovalEligibilityDto Eligibility);

internal sealed class CheckMemberRemovalEligibilityQueryHandler
    : IQueryHandler<CheckMemberRemovalEligibilityQuery, CheckMemberRemovalEligibilityQueryResult>
{
    private readonly ICompanyRepository _companyRepository;

    public CheckMemberRemovalEligibilityQueryHandler(ICompanyRepository companyRepository)
        => _companyRepository = companyRepository;

    public async Task<CheckMemberRemovalEligibilityQueryResult> HandleAsync(
        CheckMemberRemovalEligibilityQuery request,
        CancellationToken cancellationToken = default)
    {
        Company company = await _companyRepository.FindByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new NotFoundException("Company", request.CompanyId);

        bool isMember = company.Memberships.Any(m => m.LumenUserId == request.UserId);
        var eligibility = new MemberRemovalEligibilityDto(request.UserId, isMember, CanRemove: isMember);

        return new CheckMemberRemovalEligibilityQueryResult(eligibility);
    }
}
