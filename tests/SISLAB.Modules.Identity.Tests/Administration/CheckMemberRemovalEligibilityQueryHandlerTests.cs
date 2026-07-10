using SISLAB.Modules.Identity.Application.Administration;
using SISLAB.Modules.Identity.Domain.Companies;

namespace SISLAB.Modules.Identity.Tests.Administration;

/// <summary>
/// Proves the read handler that backs <c>CompanyMembersController.CheckRemovalEligibility</c>:
/// the eligibility dry-run lives here (behind the mediator), not in the controller.
/// </summary>
public sealed class CheckMemberRemovalEligibilityQueryHandlerTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-00000000000a");
    private static readonly Guid Member = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Outsider = new("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task HandleAsync_WhenUserIsMember_IsEligibleForRemoval()
    {
        Company company = Company.Seed(CompanyId, "LAFTE");
        company.AddMember(Member);

        var handler = new CheckMemberRemovalEligibilityQueryHandler(new CompanyRepositoryStub(company));

        CheckMemberRemovalEligibilityResult result =
            await handler.HandleAsync(new CheckMemberRemovalEligibilityQuery(CompanyId, Member));

        Assert.True(result.CompanyExists);
        Assert.NotNull(result.Eligibility);
        Assert.True(result.Eligibility!.IsMember);
        Assert.True(result.Eligibility.CanRemove);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsNotMember_IsNotEligible()
    {
        Company company = Company.Seed(CompanyId, "LAFTE");
        company.AddMember(Member);

        var handler = new CheckMemberRemovalEligibilityQueryHandler(new CompanyRepositoryStub(company));

        CheckMemberRemovalEligibilityResult result =
            await handler.HandleAsync(new CheckMemberRemovalEligibilityQuery(CompanyId, Outsider));

        Assert.True(result.CompanyExists);
        Assert.NotNull(result.Eligibility);
        Assert.False(result.Eligibility!.IsMember);
        Assert.False(result.Eligibility.CanRemove);
    }

    [Fact]
    public async Task HandleAsync_WhenCompanyMissing_ReportsNotFound()
    {
        var handler = new CheckMemberRemovalEligibilityQueryHandler(new CompanyRepositoryStub(company: null));

        CheckMemberRemovalEligibilityResult result =
            await handler.HandleAsync(new CheckMemberRemovalEligibilityQuery(CompanyId, Member));

        Assert.False(result.CompanyExists);
        Assert.Null(result.Eligibility);
    }
}
