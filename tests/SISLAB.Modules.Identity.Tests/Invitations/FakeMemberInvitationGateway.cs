using SISLAB.Modules.Identity.Contracts.Invitations;

namespace SISLAB.Modules.Identity.Tests.Invitations;

/// <summary>
/// Hand-rolled fake of <see cref="IMemberInvitationGateway"/>: a small e-mail -> user id map for existing
/// accounts, and a recorder for created accounts, so accept/invite handlers can run without Lumen. Fork 1 is
/// exercised by pre-seeding an existing user; the create path by leaving the e-mail unmapped.
/// </summary>
internal sealed class FakeMemberInvitationGateway : IMemberInvitationGateway
{
    private readonly Dictionary<string, Guid> _existingUsers = new(StringComparer.OrdinalIgnoreCase);

    public (string Email, string Username, string Password)? CreatedUser { get; private set; }
    public Guid CreatedUserId { get; set; } = Guid.NewGuid();

    /// <summary>Registers an existing Lumen account for the e-mail (Fork 1: link, no password).</summary>
    public FakeMemberInvitationGateway WithExistingUser(string email, Guid userId)
    {
        _existingUsers[email.Trim()] = userId;
        return this;
    }

    public Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(_existingUsers.TryGetValue(email.Trim(), out Guid id) ? id : (Guid?)null);

    public Task<Guid> CreateInvitedUserAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        CreatedUser = (email, username, password);
        _existingUsers[email.Trim()] = CreatedUserId; // now discoverable, mirroring a real insert
        return Task.FromResult(CreatedUserId);
    }
}
