using SISLAB.Modules.Identity.Application.Authorization;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Tests.Administration;
using SISLAB.SharedKernel.Exceptions;

namespace SISLAB.Modules.Identity.Tests.Authorization;

/// <summary>
/// Proves the tenant-scoped profile-assignment handlers behind <c>MemberProfilesController</c> (card [E12]
/// #104): a profile is assigned/removed only for an actual member of the active company (isolation), the
/// active company id is forwarded to Lumen as the assignment scope, and a missing company or a non-member
/// target is refused — so an operator can never reach users of another tenant.
/// </summary>
public sealed class MemberProfileAssignmentHandlerTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-0000000000c1");
    private static readonly Guid Member = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Outsider = new("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid ProfileId = new("bbbbbbbb-0000-0000-0000-0000000000b1");

    private static Company CompanyWithMember()
    {
        Company company = Company.Seed(CompanyId, "LAFTE");
        company.AddMember(Member);
        return company;
    }

    // ---- Assign -------------------------------------------------------------------------------

    [Fact]
    public async Task Assign_ToMember_ForwardsCompanyIdAsScope()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new AssignProfileToMemberCommandHandler(
            new CompanyRepositoryStub(CompanyWithMember()), gateway);

        await handler.HandleAsync(new AssignProfileToMemberCommand(CompanyId, Member, ProfileId));

        Assert.Equal((Member, ProfileId, CompanyId), gateway.AssignCall);
    }

    [Fact]
    public async Task Assign_ToNonMember_IsForbidden_AndNeverReachesLumen()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new AssignProfileToMemberCommandHandler(
            new CompanyRepositoryStub(CompanyWithMember()), gateway);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new AssignProfileToMemberCommand(CompanyId, Outsider, ProfileId)));

        Assert.Null(gateway.AssignCall);
    }

    [Fact]
    public async Task Assign_WhenCompanyMissing_ThrowsNotFound()
    {
        var handler = new AssignProfileToMemberCommandHandler(
            new CompanyRepositoryStub(company: null), new FakeLumenAuthorizationGateway());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new AssignProfileToMemberCommand(CompanyId, Member, ProfileId)));
    }

    // ---- Remove -------------------------------------------------------------------------------

    [Fact]
    public async Task Remove_FromMember_ForwardsCompanyIdAsScope()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new RemoveProfileFromMemberCommandHandler(
            new CompanyRepositoryStub(CompanyWithMember()), gateway);

        await handler.HandleAsync(new RemoveProfileFromMemberCommand(CompanyId, Member, ProfileId));

        Assert.Equal((Member, ProfileId, CompanyId), gateway.RemoveCall);
    }

    [Fact]
    public async Task Remove_FromNonMember_IsForbidden_AndNeverReachesLumen()
    {
        var gateway = new FakeLumenAuthorizationGateway();
        var handler = new RemoveProfileFromMemberCommandHandler(
            new CompanyRepositoryStub(CompanyWithMember()), gateway);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.HandleAsync(new RemoveProfileFromMemberCommand(CompanyId, Outsider, ProfileId)));

        Assert.Null(gateway.RemoveCall);
    }

    [Fact]
    public async Task Remove_WhenCompanyMissing_ThrowsNotFound()
    {
        var handler = new RemoveProfileFromMemberCommandHandler(
            new CompanyRepositoryStub(company: null), new FakeLumenAuthorizationGateway());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new RemoveProfileFromMemberCommand(CompanyId, Member, ProfileId)));
    }
}
