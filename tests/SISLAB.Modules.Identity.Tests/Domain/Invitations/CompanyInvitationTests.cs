using SISLAB.Modules.Identity.Domain.Invitations;
using SISLAB.Modules.Identity.Domain.Invitations.Events;
using SISLAB.SharedKernel.Domain;
using SISLAB.SharedKernel.Exceptions;
using SISLAB.TestSupport;

namespace SISLAB.Modules.Identity.Tests.Domain.Invitations;

/// <summary>
/// Unit tests for the <see cref="CompanyInvitation"/> aggregate (card [E12] #75c): issuing, state transitions,
/// lazy expiry, token hashing/matching and resend idempotency — all without a database.
/// </summary>
public sealed class CompanyInvitationTests
{
    private static readonly Guid CompanyId = new("10000000-0000-0000-0000-0000000000c1");
    private static readonly Guid ProfileId = new("bbbbbbbb-0000-0000-0000-0000000000b1");
    private static readonly Guid InviterId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly FixedClock Clock = FixedClock.On(2026, 7, 14);

    private static CompanyInvitation Issue(string email = "invitee@lab.test") =>
        CompanyInvitation.Issue(CompanyId, email, ProfileId, InviterId, Clock);

    // ---- Issue --------------------------------------------------------------------------------

    [Fact]
    public void Issue_CreatesPendingInvitation_WithNormalizedEmailAndSevenDayWindow()
    {
        CompanyInvitation invitation = Issue("  Invitee@LAB.test  ");

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(CompanyId, invitation.CompanyId);
        Assert.Equal("invitee@lab.test", invitation.Email); // trimmed + lowercased
        Assert.Equal(ProfileId, invitation.ProfileId);
        Assert.Equal(InviterId, invitation.InvitedByUserId);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal(Clock.UtcNow, invitation.CreatedAt);
        Assert.Equal(Clock.UtcNow.Add(CompanyInvitation.DefaultLifetime), invitation.ExpiresAt);
        Assert.Null(invitation.AcceptedAt);
    }

    [Fact]
    public void Issue_RaisesMemberInvited_CarryingTheRawToken()
    {
        CompanyInvitation invitation = Issue();

        IDomainEvent domainEvent = Assert.Single(invitation.DomainEvents);
        MemberInvited invited = Assert.IsType<MemberInvited>(domainEvent);

        Assert.Equal(invitation.Id, invited.InvitationId);
        Assert.Equal(CompanyId, invited.CompanyId);
        Assert.Equal("invitee@lab.test", invited.Email);
        Assert.Equal(ProfileId, invited.ProfileId);
        Assert.Equal(InviterId, invited.InvitedByUserId);
        Assert.False(string.IsNullOrWhiteSpace(invited.RawToken));
        // The raw token is the secret: presenting it must match this invitation's stored hash.
        Assert.True(invitation.MatchesToken(invited.RawToken));
    }

    [Fact]
    public void Issue_DoesNotStoreTheRawToken_OnlyItsHash()
    {
        CompanyInvitation invitation = Issue();
        string rawToken = ((MemberInvited)invitation.DomainEvents[0]).RawToken;

        // The persisted hash is not the raw token, and a wrong token does not match.
        Assert.NotEqual(rawToken, invitation.Token.TokenHash);
        Assert.False(invitation.MatchesToken("not-the-token"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Issue_WithEmptyEmail_Throws(string email) =>
        Assert.Throws<ArgumentException>(() =>
            CompanyInvitation.Issue(CompanyId, email, ProfileId, InviterId, Clock));

    [Fact]
    public void Issue_WithEmptyProfile_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            CompanyInvitation.Issue(CompanyId, "x@y.z", Guid.Empty, InviterId, Clock));

    [Fact]
    public void Issue_WithEmptyInviter_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            CompanyInvitation.Issue(CompanyId, "x@y.z", ProfileId, Guid.Empty, Clock));

    // ---- Accept -------------------------------------------------------------------------------

    [Fact]
    public void Accept_WhilePendingAndNotExpired_TransitionsToAccepted()
    {
        CompanyInvitation invitation = Issue();

        invitation.Accept(Clock);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(Clock.UtcNow, invitation.AcceptedAt);
    }

    [Fact]
    public void Accept_Twice_Throws_Conflict_SoNoDoubleAcceptSucceeds()
    {
        CompanyInvitation invitation = Issue();
        invitation.Accept(Clock);

        Assert.Throws<ConflictException>(() => invitation.Accept(Clock));
    }

    [Fact]
    public void Accept_AfterExpiry_FlipsToExpired_AndThrows()
    {
        CompanyInvitation invitation = Issue();
        var afterExpiry = new FixedClock(invitation.ExpiresAt.AddSeconds(1));

        Assert.Throws<BusinessException>(() => invitation.Accept(afterExpiry));
        Assert.Equal(InvitationStatus.Expired, invitation.Status);
    }

    [Fact]
    public void Accept_ExactlyAtExpiry_IsRejected()
    {
        CompanyInvitation invitation = Issue();
        var atExpiry = new FixedClock(invitation.ExpiresAt);

        Assert.Throws<BusinessException>(() => invitation.Accept(atExpiry));
    }

    // ---- Revoke -------------------------------------------------------------------------------

    [Fact]
    public void Revoke_WhilePending_TransitionsToRevoked()
    {
        CompanyInvitation invitation = Issue();

        invitation.Revoke();

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
    }

    [Fact]
    public void Revoke_AfterAccept_Throws()
    {
        CompanyInvitation invitation = Issue();
        invitation.Accept(Clock);

        Assert.Throws<ConflictException>(invitation.Revoke);
    }

    // ---- Reissue (resend) ---------------------------------------------------------------------

    [Fact]
    public void Reissue_MintsNewTokenAndResetsWindow_RaisingMemberInvitedAgain()
    {
        CompanyInvitation invitation = Issue();
        string firstToken = ((MemberInvited)invitation.DomainEvents[0]).RawToken;
        invitation.ClearDomainEvents();

        var later = new FixedClock(Clock.UtcNow.AddDays(1));
        var newProfile = new Guid("bbbbbbbb-0000-0000-0000-0000000000b2");

        invitation.Reissue(newProfile, later);

        // New token invalidates the old one; profile and window are updated; a fresh event is raised.
        string secondToken = ((MemberInvited)Assert.Single(invitation.DomainEvents)).RawToken;
        Assert.NotEqual(firstToken, secondToken);
        Assert.False(invitation.MatchesToken(firstToken));
        Assert.True(invitation.MatchesToken(secondToken));
        Assert.Equal(newProfile, invitation.ProfileId);
        Assert.Equal(later.UtcNow.Add(CompanyInvitation.DefaultLifetime), invitation.ExpiresAt);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Reissue_AfterAccept_Throws()
    {
        CompanyInvitation invitation = Issue();
        invitation.Accept(Clock);

        Assert.Throws<ConflictException>(() => invitation.Reissue(ProfileId, Clock));
    }

    // ---- IsAcceptable / IsExpired -------------------------------------------------------------

    [Fact]
    public void IsAcceptable_IsTrueWhilePendingAndBeforeExpiry_FalseOtherwise()
    {
        CompanyInvitation invitation = Issue();

        Assert.True(invitation.IsAcceptable(Clock.UtcNow));
        Assert.False(invitation.IsAcceptable(invitation.ExpiresAt));

        invitation.Revoke();
        Assert.False(invitation.IsAcceptable(Clock.UtcNow));
    }
}
