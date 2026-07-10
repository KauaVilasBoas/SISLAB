using SISLAB.Modules.Identity.Contracts.Administration;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Identity.Application.Administration;

/// <summary>Dry-runs removal eligibility for a member of the active company.</summary>
/// <param name="CompanyId">Active company resolved from <c>ITenantContext</c>, never from the client.</param>
/// <param name="UserId">Lumen user to evaluate.</param>
public sealed record CheckMemberRemovalEligibilityQuery(Guid CompanyId, Guid UserId)
    : IQuery<CheckMemberRemovalEligibilityResult>;

/// <param name="CompanyExists">
/// False when no company matches the id (distinguishes 404 from a non-member result).
/// </param>
public sealed record CheckMemberRemovalEligibilityResult(
    bool CompanyExists,
    MemberRemovalEligibilityDto? Eligibility)
{
    public static CheckMemberRemovalEligibilityResult NotFound() => new(false, null);
}

internal sealed class CheckMemberRemovalEligibilityQueryHandler
    : IQueryHandler<CheckMemberRemovalEligibilityQuery, CheckMemberRemovalEligibilityResult>
{
    private readonly ICompanyRepository _companyRepository;

    public CheckMemberRemovalEligibilityQueryHandler(ICompanyRepository companyRepository)
        => _companyRepository = companyRepository;

    public async Task<CheckMemberRemovalEligibilityResult> HandleAsync(
        CheckMemberRemovalEligibilityQuery request,
        CancellationToken cancellationToken = default)
    {
        Company? company = await _companyRepository.FindByIdAsync(request.CompanyId, cancellationToken);
        if (company is null)
            return CheckMemberRemovalEligibilityResult.NotFound();

        bool isMember = company.Memberships.Any(m => m.LumenUserId == request.UserId);
        var eligibility = new MemberRemovalEligibilityDto(request.UserId, isMember, CanRemove: isMember);

        return new CheckMemberRemovalEligibilityResult(true, eligibility);
    }
}
