using SISLAB.Modules.Identity.Application.Invitations;
using SISLAB.Modules.Identity.Contracts.Authorization;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Domain.Invitations;
using SISLAB.Modules.Identity.Domain.Invitations.Events;
using SISLAB.Modules.Identity.Tests.Authorization;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Identity.Tests.Invitations;

/// <summary>
/// Proves the invite use case (card [E12] #75c): it validates the active company and profile, refuses inviting
/// an existing member, creates a pending invitation raising <see cref="MemberInvited"/>, and rehydrates an
/// existing pending invitation on resend (idempotent — no duplicate).
/// </summary>
public sealed class InviteMemberCommandHandlerTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-0000000000c1");
    private static readonly Guid ProfileId = new("bbbbbbbb-0000-0000-0000-0000000000b1");
    private static readonly Guid InviterId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly FixedClock Clock = FixedClock.On(2026, 7, 14);

    private static Company Company() => global::SISLAB.Modules.Identity.Domain.Companies.Company.Seed(CompanyId, "LAFTE");

    private static ProfileDto ExistingProfile() => new(ProfileId, "Operador", "", IsSystem: false);

    private static (InviteMemberCommandHandler Handler,
        FakeCompanyInvitationRepository Invitations,
        FakeMemberInvitationGateway Gateway) BuildHandler(
            Company company,
            FakeLumenAuthorizationGateway? authorization = null,
            FakeMemberInvitationGateway? gateway = null)
    {
        authorization ??= new FakeLumenAuthorizationGateway { ProfileToReturn = ExistingProfile() };
        gateway ??= new FakeMemberInvitationGateway();
        var invitations = new FakeCompanyInvitationRepository();
        var companies = new FakeCompanyRepository(company);

        var handler = new InviteMemberCommandHandler(companies, invitations, authorization, gateway, Clock);
        return (handler, invitations, gateway);
    }

    [Fact]
    public async Task Invite_NewEmail_CreatesPendingInvitation_AndRaisesMemberInvited()
    {
        (InviteMemberCommandHandler handler, FakeCompanyInvitationRepository invitations, _) =
            BuildHandler(Company());

        InviteMemberResult result = await handler.HandleAsync(
            new InviteMemberCommand(CompanyId, "new@lab.test", ProfileId, InviterId));

        Assert.False(result.Resent);
        CompanyInvitation created = Assert.Single(invitations.Added);
        Assert.Equal(result.InvitationId, created.Id);
        Assert.Equal("new@lab.test", created.Email);
        Assert.Equal(ProfileId, created.ProfileId);
        Assert.Equal(InvitationStatus.Pending, created.Status);
        Assert.IsType<MemberInvited>(Assert.Single(created.DomainEvents));
    }

    [Fact]
    public async Task Invite_WhenProfileMissing_ThrowsNotFound_AndCreatesNothing()
    {
        var authorization = new FakeLumenAuthorizationGateway { ProfileToReturn = null };
        (InviteMemberCommandHandler handler, FakeCompanyInvitationRepository invitations, _) =
            BuildHandler(Company(), authorization);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new InviteMemberCommand(CompanyId, "x@lab.test", ProfileId, InviterId)));

        Assert.Empty(invitations.Added);
    }

    [Fact]
    public async Task Invite_WhenCompanyMissing_ThrowsNotFound()
    {
        var companies = new FakeCompanyRepository(); // empty
        var handler = new InviteMemberCommandHandler(
            companies,
            new FakeCompanyInvitationRepository(),
            new FakeLumenAuthorizationGateway { ProfileToReturn = ExistingProfile() },
            new FakeMemberInvitationGateway(),
            Clock);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new InviteMemberCommand(CompanyId, "x@lab.test", ProfileId, InviterId)));
    }

    [Fact]
    public async Task Invite_ExistingMember_ThrowsConflict()
    {
        Guid existingUserId = Guid.NewGuid();
        Company company = Company();
        company.AddMember(existingUserId);

        var gateway = new FakeMemberInvitationGateway().WithExistingUser("member@lab.test", existingUserId);
        (InviteMemberCommandHandler handler, FakeCompanyInvitationRepository invitations, _) =
            BuildHandler(company, gateway: gateway);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.HandleAsync(new InviteMemberCommand(CompanyId, "member@lab.test", ProfileId, InviterId)));

        Assert.Empty(invitations.Added);
    }

    [Fact]
    public async Task Invite_ExistingUserWhoIsMemberElsewhere_IsAllowed_NotTreatedAsMember()
    {
        // The user has an account and is a member of ANOTHER company, but not of this one: inviting is allowed.
        Guid userElsewhere = Guid.NewGuid();
        var gateway = new FakeMemberInvitationGateway().WithExistingUser("elsewhere@lab.test", userElsewhere);
        (InviteMemberCommandHandler handler, FakeCompanyInvitationRepository invitations, _) =
            BuildHandler(Company(), gateway: gateway);

        InviteMemberResult result = await handler.HandleAsync(
            new InviteMemberCommand(CompanyId, "elsewhere@lab.test", ProfileId, InviterId));

        Assert.False(result.Resent);
        Assert.Single(invitations.Added);
    }

    [Fact]
    public async Task Invite_ResendToPendingEmail_RehydratesExisting_NoDuplicate()
    {
        (InviteMemberCommandHandler handler, FakeCompanyInvitationRepository invitations, _) =
            BuildHandler(Company());

        InviteMemberResult first = await handler.HandleAsync(
            new InviteMemberCommand(CompanyId, "resend@lab.test", ProfileId, InviterId));
        InviteMemberResult second = await handler.HandleAsync(
            new InviteMemberCommand(CompanyId, "resend@lab.test", ProfileId, InviterId));

        Assert.False(first.Resent);
        Assert.True(second.Resent);
        Assert.Equal(first.InvitationId, second.InvitationId); // same aggregate re-sent
        Assert.Single(invitations.Added);                       // only one ever created
        Assert.Single(invitations.Updated);                     // the resend updated it
    }
}
