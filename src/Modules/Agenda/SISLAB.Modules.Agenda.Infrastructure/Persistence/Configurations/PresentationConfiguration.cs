using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Agenda.Domain.Presentations;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class PresentationConfiguration : IEntityTypeConfiguration<Presentation>
{
    public void Configure(EntityTypeBuilder<Presentation> builder)
    {
        builder.ToTable("presentations");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.Title).IsRequired().HasMaxLength(400);
        builder.Property(p => p.Doi).HasMaxLength(200);
        builder.Property(p => p.PresenterName).IsRequired().HasMaxLength(200);
        builder.Property(p => p.ScheduledDate).IsRequired();
        builder.Property(p => p.ReminderSentAt);
        builder.Property(p => p.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Notes).HasMaxLength(1000);
        builder.Property(p => p.CreatedAtUtc).IsRequired();

        builder.HasIndex(p => new { p.CompanyId, p.ScheduledDate })
            .HasDatabaseName("ix_presentations_company_date");
    }
}
