using SISLAB.Modules.Identity.Application.Invitations;
using SISLAB.Modules.Identity.Domain.Companies;
using SISLAB.Modules.Identity.Domain.Invitations;
using SISLAB.Modules.Identity.Domain.Invitations.Events;
using SISLAB.Modules.Identity.Tests.Authorization;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Identity.Tests.Invitations;

/// <summary>
/// Proves the accept use case (card [E12] #75c): linking an existing account without a password (Fork 1),
/// creating one for a brand-new invitee, granting the invited profile scoped to the invited company only
/// (multi-company isolation), idempotent double-accept (no duplicate membership), and rejecting an expired
/// invitation.
/// </summary>
public sealed class AcceptInvitationCommandHandlerTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-0000000000c1");
    private static readonly Guid OtherCompanyId = new("20000000-0000-0000-0000-0000000000c2");
    private static readonly Guid ProfileId = new("bbbbbbbb-0000-0000-0000-0000000000b1");
    private static readonly Guid InviterId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly FixedClock Clock = FixedClock.On(2026, 7, 14);

    private static (CompanyInvitation Invitation, string RawToken) NewInvitation(
        Guid companyId,
        string email = "invitee@lab.test")
    {
        CompanyInvitation invitation = CompanyInvitation.Issue(companyId, email, ProfileId, InviterId, Clock);
        string rawToken = ((MemberInvited)invitation.DomainEvents[0]).RawToken;
        invitation.ClearDomainEvents();
        return (invitation, rawToken);
    }

    private static AcceptInvitationCommandHandler BuildHandler(
        CompanyInvitation invitation,
        Company company,
        FakeMemberInvitationGateway gateway,
        FakeLumenAuthorizationGateway authorization,
        FakeCompanyInvitationRepository? invitations = null,
        FakeCompanyRepository? companies = null)
    {
        invitations ??= new FakeCompanyInvitationRepository();
        invitations.Seed(invitation);
        companies ??= new FakeCompanyRepository(company);
        return new AcceptInvitationCommandHandler(invitations, companies, gateway, authorization, Clock);
    }

    // ---- Fork 1: link existing account (no password) ------------------------------------------

    [Fact]
    public async Task Accept_WhenEmailAlreadyHasAccount_LinksIt_NoPasswordNeeded()
    {
        Guid existingUserId = Guid.NewGuid();
        (CompanyInvitation invitation, string token) = NewInvitation(CompanyId);
        Company company = Company.Seed(CompanyId, "LAFTE");
        var gateway = new FakeMemberInvitationGateway().WithExistingUser("invitee@lab.test", existingUserId);
        var authorization = new FakeLumenAuthorizationGateway();

        AcceptInvitationCommandHandler handler = BuildHandler(invitation, company, gateway, authorization);

        // No username/password supplied — linking must not require them.
        AcceptInvitationResult result = await handler.HandleAsync(
            new AcceptInvitationCommand(token, Username: null, Password: null));

        Assert.False(result.AccountCreated);
        Assert.Equal(existingUserId, result.UserId);
        Assert.Equal(CompanyId, result.CompanyId);
        Assert.Null(gateway.CreatedUser);               // no account was created
        Assert.True(company.IsMember(existingUserId));  // membership added
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal((existingUserId, ProfileId, CompanyId), authorization.AssignCall); // scoped to THIS company
    }

    // ---- Create new account for a brand-new invitee -------------------------------------------

    [Fact]
    public async Task Accept_WhenEmailHasNoAccount_CreatesOneFromUsernameAndPassword()
    {
        (CompanyInvitation invitation, string token) = NewInvitation(CompanyId);
        Company company = Company.Seed(CompanyId, "LAFTE");
        var gateway = new FakeMemberInvitationGateway { CreatedUserId = Guid.NewGuid() };
        var authorization = new FakeLumenAuthorizationGateway();

        AcceptInvitationCommandHandler handler = BuildHandler(invitation, company, gateway, authorization);

        AcceptInvitationResult result = await handler.HandleAsync(
            new AcceptInvitationCommand(token, Username: "newbie", Password: "Str0ng-Password!"));

        Assert.True(result.AccountCreated);
        Assert.Equal(gateway.CreatedUserId, result.UserId);
        Assert.Equal(("invitee@lab.test", "newbie", "Str0ng-Password!"), gateway.CreatedUser);
        Assert.True(company.IsMember(gateway.CreatedUserId));
        Assert.Equal((gateway.CreatedUserId, ProfileId, CompanyId), authorization.AssignCall);
    }

    [Fact]
    public async Task Accept_WhenEmailHasNoAccount_AndNoCredentials_ThrowsBusinessException()
    {
        (CompanyInvitation invitation, string token) = NewInvitation(CompanyId);
        Company company = Company.Seed(CompanyId, "LAFTE");
        var gateway = new FakeMemberInvitationGateway(); // unmapped e-mail = no existing account
        AcceptInvitationCommandHandler handler =
            BuildHandler(invitation, company, gateway, new FakeLumenAuthorizationGateway());

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new AcceptInvitationCommand(token, Username: null, Password: null)));
    }

    // ---- Idempotent double-accept -------------------------------------------------------------

    [Fact]
    public async Task Accept_Twice_IsIdempotent_NoDuplicateMembership_NoSecondProfileGrant()
    {
        Guid existingUserId = Guid.NewGuid();
        (CompanyInvitation invitation, string token) = NewInvitation(CompanyId);
        Company company = Company.Seed(CompanyId, "LAFTE");
        var gateway = new FakeMemberInvitationGateway().WithExistingUser("invitee@lab.test", existingUserId);
        var authorization = new FakeLumenAuthorizationGateway();
        AcceptInvitationCommandHandler handler = BuildHandler(invitation, company, gateway, authorization);

        await handler.HandleAsync(new AcceptInvitationCommand(token, null, null));
        authorization.ResetAssignCall();

        AcceptInvitationResult second = await handler.HandleAsync(
            new AcceptInvitationCommand(token, null, null));

        // Second accept succeeds idempotently and mutates nothing more.
        Assert.Equal(existingUserId, second.UserId);
        Assert.Equal(CompanyId, second.CompanyId);
        Assert.Single(company.Memberships);          // exactly one membership, no duplicate
        Assert.Null(authorization.AssignCall);        // profile not re-granted on the second accept
    }

    // ---- Multi-company isolation --------------------------------------------------------------

    [Fact]
    public async Task Accept_GrantsProfileOnlyInInvitedCompany_NotAnother()
    {
        Guid existingUserId = Guid.NewGuid();
        // Invitation is for CompanyId; another company also exists in the store.
        (CompanyInvitation invitation, string token) = NewInvitation(CompanyId);
        Company invited = Company.Seed(CompanyId, "LAFTE");
        Company other = Company.Seed(OtherCompanyId, "Other Lab");
        var gateway = new FakeMemberInvitationGateway().WithExistingUser("invitee@lab.test", existingUserId);
        var authorization = new FakeLumenAuthorizationGateway();
        var companies = new FakeCompanyRepository(invited, other);

        AcceptInvitationCommandHandler handler =
            BuildHandler(invitation, invited, gateway, authorization, companies: companies);

        await handler.HandleAsync(new AcceptInvitationCommand(token, null, null));

        // Membership and profile land in the invited company only; the other tenant is untouched.
        Assert.True(invited.IsMember(existingUserId));
        Assert.False(other.IsMember(existingUserId));
        Assert.Equal((existingUserId, ProfileId, CompanyId), authorization.AssignCall);
    }

    // ---- Token/expiry guards ------------------------------------------------------------------

    [Fact]
    public async Task Accept_WithUnknownToken_ThrowsNotFound()
    {
        (CompanyInvitation invitation, _) = NewInvitation(CompanyId);
        AcceptInvitationCommandHandler handler = BuildHandler(
            invitation, Company.Seed(CompanyId, "LAFTE"),
            new FakeMemberInvitationGateway(), new FakeLumenAuthorizationGateway());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.HandleAsync(new AcceptInvitationCommand("totally-wrong-token", null, null)));
    }

    [Fact]
    public async Task Accept_AfterExpiry_ThrowsBusinessException_AndGrantsNothing()
    {
        Guid existingUserId = Guid.NewGuid();
        (CompanyInvitation invitation, string token) = NewInvitation(CompanyId);
        Company company = Company.Seed(CompanyId, "LAFTE");
        var gateway = new FakeMemberInvitationGateway().WithExistingUser("invitee@lab.test", existingUserId);
        var authorization = new FakeLumenAuthorizationGateway();

        var afterExpiry = new FixedClock(invitation.ExpiresAt.AddSeconds(1));
        var invitations = new FakeCompanyInvitationRepository();
        invitations.Seed(invitation);
        var handler = new AcceptInvitationCommandHandler(
            invitations, new FakeCompanyRepository(company), gateway, authorization, afterExpiry);

        await Assert.ThrowsAsync<BusinessException>(() =>
            handler.HandleAsync(new AcceptInvitationCommand(token, null, null)));

        Assert.False(company.IsMember(existingUserId));
        Assert.Null(authorization.AssignCall);
    }
}
