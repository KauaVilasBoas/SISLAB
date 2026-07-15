using Microsoft.EntityFrameworkCore;
using SISLAB.Modules.Identity.Domain.Invitations;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICompanyInvitationRepository"/> (schema <c>tenancy</c>).
///
/// <para>The <see cref="IdentityDbContext"/> applies no tenant query filter (the <c>Company</c>/invitation
/// tables are the tenancy root, not tenant-scoped entities), so token-hash lookups resolve across companies —
/// which the accept/preview flows need, since they run with no active tenant and rely on the token as the
/// secret. Tenant isolation for invitations is enforced instead by the invite use case (which stamps the
/// active <c>CompanyId</c>) and by scoping the granted profile to that company on accept.</para>
/// </summary>
internal sealed class CompanyInvitationRepository : ICompanyInvitationRepository
{
    private readonly IdentityDbContext _dbContext;

    public CompanyInvitationRepository(IdentityDbContext dbContext) => _dbContext = dbContext;

    public Task<CompanyInvitation?> FindPendingByEmailAsync(
        Guid companyId,
        string email,
        CancellationToken ct = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        return _dbContext.CompanyInvitations
            .FirstOrDefaultAsync(
                i => i.CompanyId == companyId
                    && i.Email == normalized
                    && i.Status == InvitationStatus.Pending,
                ct);
    }

    public Task<CompanyInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        // Capture the value object once so EF compares it via the value converter as a single parameter —
        // this translates to "token_hash = @p", a server-side unique-index lookup (no row materialization).
        InvitationToken token = InvitationToken.FromHash(tokenHash);
        return _dbContext.CompanyInvitations
            .FirstOrDefaultAsync(i => i.Token == token, ct);
    }

    public async Task AddAsync(CompanyInvitation invitation, CancellationToken ct = default)
        => await _dbContext.CompanyInvitations.AddAsync(invitation, ct);

    public Task UpdateAsync(CompanyInvitation invitation, CancellationToken ct = default)
    {
        _dbContext.CompanyInvitations.Update(invitation);
        return Task.CompletedTask;
    }
}
