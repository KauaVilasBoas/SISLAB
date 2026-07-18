using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Agenda.Domain.Bioterium;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class BioteriumConfiguration : IEntityTypeConfiguration<BioteriumAssignment>
{
    public void Configure(EntityTypeBuilder<BioteriumAssignment> builder)
    {
        builder.ToTable("bioterium_assignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.CompanyId).IsRequired();
        builder.Property(a => a.AssignmentDate).IsRequired();
        builder.Property(a => a.ResponsibleName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.SwappedFromName).HasMaxLength(200);
        builder.Property(a => a.SwapReason).HasMaxLength(500);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.CreatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.CompanyId, a.AssignmentDate })
            .HasDatabaseName("ix_bioterium_assignments_company_date");
    }
}
