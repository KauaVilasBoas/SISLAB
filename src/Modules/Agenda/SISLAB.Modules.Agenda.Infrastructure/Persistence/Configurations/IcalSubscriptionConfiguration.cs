using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Agenda.Domain.Subscriptions;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the <see cref="IcalSubscription"/> aggregate (card [E10.10]) into
/// <c>agenda.ical_subscriptions</c>. One subscription per (company, user): the composite unique index enforces
/// the invariant, and the token carries its own lookup index because the public <c>.ics</c> feed resolves a
/// subscription by token alone.
/// </summary>
internal sealed class IcalSubscriptionConfiguration : IEntityTypeConfiguration<IcalSubscription>
{
    public void Configure(EntityTypeBuilder<IcalSubscription> builder)
    {
        builder.ToTable("ical_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.CompanyId).IsRequired();
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Token).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
        builder.Property(s => s.UpdatedAtUtc).IsRequired();

        builder.HasIndex(s => new { s.CompanyId, s.UserId })
            .IsUnique()
            .HasDatabaseName("ux_ical_subscriptions_company_user");

        builder.HasIndex(s => s.Token)
            .IsUnique()
            .HasDatabaseName("ux_ical_subscriptions_token");
    }
}
