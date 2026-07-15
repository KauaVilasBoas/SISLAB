using Lumen.Identity.Domain.Security;
using Lumen.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SISLAB.Modules.Identity.Contracts.Invitations;

namespace SISLAB.Modules.Identity.Infrastructure.Invitations;

/// <summary>
/// Lumen-backed implementation of <see cref="IMemberInvitationGateway"/> (card [E12] #75c): the adapter that
/// resolves or provisions the invitee's Lumen account when an invitation is accepted, using Lumen's domain API
/// directly — the same proven path as the signup onboarding gateway.
///
/// <para>Lumen Identity persists through its own internal DbContext, which exposes no public interface, so it is
/// resolved by type from the container (as elsewhere in this module). No cross-boundary FK is touched; the
/// invitee's Lumen user id crosses into SISLAB's tenancy store only by value.</para>
///
/// <para>New invitee accounts are created already active (<see cref="User.ConfirmEmail"/>) so the invitee can
/// authenticate immediately after accepting — the e-mail confirmation flow is broken in Lumen.Identity 1.0.0
/// and, more importantly, following an invitation already proves ownership of the e-mail.</para>
/// </summary>
internal sealed class LumenMemberInvitationGateway : IMemberInvitationGateway
{
    // Lumen's internal Identity DbContext, resolved by type (no public interface exposes it) — same seam the
    // signup onboarding gateway and dev seeder use to commit Lumen writes.
    private static readonly Type LumenIdentityDbContextType =
        typeof(IUserRepository).Assembly.GetType("Lumen.Identity.Persistence.IdentityDbContext")!;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IServiceProvider _serviceProvider;

    public LumenMemberInvitationGateway(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IServiceProvider serviceProvider)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _serviceProvider = serviceProvider;
    }

    public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        User? existing = await _users.FindByEmailAsync(email.Trim(), cancellationToken);
        return existing?.Id;
    }

    public async Task<Guid> CreateInvitedUserAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        string passwordHash = _passwordHasher.Hash(password);
        User invitee = User.Create(email.Trim(), username.Trim(), passwordHash);
        invitee.ConfirmEmail(); // active immediately — accepting the invitation proves e-mail ownership

        await _users.InsertAsync(invitee, cancellationToken);
        await SaveLumenIdentityAsync(cancellationToken);

        return invitee.Id;
    }

    private Task SaveLumenIdentityAsync(CancellationToken cancellationToken)
        => ((DbContext)_serviceProvider.GetRequiredService(LumenIdentityDbContextType))
            .SaveChangesAsync(cancellationToken);
}
