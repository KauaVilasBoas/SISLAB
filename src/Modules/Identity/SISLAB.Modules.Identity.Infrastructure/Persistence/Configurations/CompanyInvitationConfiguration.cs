using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Identity.Domain.Invitations;

namespace SISLAB.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="CompanyInvitation"/> aggregate (schema <c>tenancy</c>).
///
/// <para>The <see cref="InvitationToken"/> value object is stored as its hash in a single <c>token_hash</c>
/// column via a value converter — the raw token is never persisted. Both <c>CompanyId</c> and <c>ProfileId</c>
/// are value references (plain <c>uuid</c> columns, no cross-schema FK), keeping the tenancy store isolated from
/// Lumen's schema.</para>
///
/// <para>The "at most one pending invitation per (company, e-mail)" invariant is enforced at the database by a
/// <b>partial unique index</b> filtered to <c>status = 0</c> (Pending) — so a race that tries to create a second
/// pending invitation for the same pair fails at commit, complementing the handler's rehydrate-on-resend logic.</para>
/// </summary>
internal sealed class CompanyInvitationConfiguration : IEntityTypeConfiguration<CompanyInvitation>
{
    public void Configure(EntityTypeBuilder<CompanyInvitation> builder)
    {
        builder.ToTable("company_invitations", schema: "tenancy");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.CompanyId)
            .IsRequired();

        builder.Property(i => i.Email)
            .IsRequired()
            .HasMaxLength(256);

        // Value reference: profile_id is a plain uuid with no external FK to Lumen's schema.
        builder.Property(i => i.ProfileId)
            .IsRequired();

        // InvitationToken <-> token_hash (only the hash is stored; raw token never persisted).
        builder.Property(i => i.Token)
            .HasConversion(
                token => token.TokenHash,
                hash => InvitationToken.FromHash(hash))
            .HasColumnName("token_hash")
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(i => i.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.ExpiresAt)
            .IsRequired();

        builder.Property(i => i.AcceptedAt);

        builder.Property(i => i.InvitedByUserId)
            .IsRequired();

        // Accept/preview lookup is by token hash — unique so a hash resolves to exactly one invitation.
        builder.HasIndex(i => i.Token)
            .IsUnique()
            .HasDatabaseName("ix_company_invitations_token_hash");

        // One outstanding pending invitation per (company, e-mail): partial unique index over status = Pending.
        builder.HasIndex(i => new { i.CompanyId, i.Email })
            .IsUnique()
            .HasFilter("status = 0")
            .HasDatabaseName("ix_company_invitations_company_email_pending");
    }
}
