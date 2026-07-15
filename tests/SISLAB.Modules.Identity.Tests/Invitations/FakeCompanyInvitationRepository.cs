using SISLAB.Modules.Identity.Domain.Invitations;

namespace SISLAB.Modules.Identity.Tests.Invitations;

/// <summary>
/// In-memory <see cref="ICompanyInvitationRepository"/> for handler tests: keeps invitations in a list and
/// records Add/Update calls, so a handler can be exercised without a database. Lookups mirror the EF impl
/// (pending-by-email is normalized; by-token-hash matches the stored hash).
/// </summary>
internal sealed class FakeCompanyInvitationRepository : ICompanyInvitationRepository
{
    public List<CompanyInvitation> Store { get; } = [];
    public List<CompanyInvitation> Added { get; } = [];
    public List<CompanyInvitation> Updated { get; } = [];

    public void Seed(CompanyInvitation invitation) => Store.Add(invitation);

    public Task<CompanyInvitation?> FindPendingByEmailAsync(
        Guid companyId,
        string email,
        CancellationToken ct = default)
    {
        string normalized = email.Trim().ToLowerInvariant();
        CompanyInvitation? match = Store.FirstOrDefault(i =>
            i.CompanyId == companyId
            && i.Email == normalized
            && i.Status == InvitationStatus.Pending);
        return Task.FromResult(match);
    }

    public Task<CompanyInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        CompanyInvitation? match = Store.FirstOrDefault(i => i.Token.TokenHash == tokenHash);
        return Task.FromResult(match);
    }

    public Task AddAsync(CompanyInvitation invitation, CancellationToken ct = default)
    {
        Store.Add(invitation);
        Added.Add(invitation);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CompanyInvitation invitation, CancellationToken ct = default)
    {
        Updated.Add(invitation);
        return Task.CompletedTask;
    }
}
