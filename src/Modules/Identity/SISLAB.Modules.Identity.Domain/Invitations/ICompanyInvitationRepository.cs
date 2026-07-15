namespace SISLAB.Modules.Identity.Domain.Invitations;

/// <summary>
/// Repository for the <see cref="CompanyInvitation"/> aggregate. Concrete implementation lives in the
/// module's Infrastructure project (schema <c>tenancy</c>).
/// </summary>
public interface ICompanyInvitationRepository
{
    /// <summary>
    /// Finds the outstanding <see cref="InvitationStatus.Pending"/> invitation for a (company, e-mail) pair,
    /// or <see langword="null"/> when none exists. Used by the invite use case to rehydrate and re-send an
    /// existing invitation instead of creating a duplicate (idempotent resend, backed by the partial unique
    /// index). E-mail is matched normalized.
    /// </summary>
    Task<CompanyInvitation?> FindPendingByEmailAsync(
        Guid companyId,
        string email,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves an invitation by the hash of a presented raw token, or <see langword="null"/> when no
    /// invitation carries that hash. This is the accept/preview lookup — the raw token is hashed by the caller
    /// (the aggregate's <see cref="InvitationToken"/>) and the comparison is on the stored hash, never the
    /// secret. Not tenant-scoped: acceptance happens with no active company, and the token itself is the secret.
    /// </summary>
    Task<CompanyInvitation?> FindByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(CompanyInvitation invitation, CancellationToken ct = default);

    Task UpdateAsync(CompanyInvitation invitation, CancellationToken ct = default);
}
