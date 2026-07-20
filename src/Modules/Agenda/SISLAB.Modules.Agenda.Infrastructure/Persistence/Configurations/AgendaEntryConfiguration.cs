using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Agenda.Domain.Entries;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF mapping for the <see cref="AgendaEntry"/> aggregate (card [E10.1] #1) into <c>agenda.agenda_entries</c>.
/// The recurrence rule value object is stored as its canonical text; the excluded-dates set is stored as a
/// <c>jsonb</c> array of <c>DateOnly</c> — a small, entry-owned collection that is always loaded with its
/// parent, so a side table would only add joins without buying queryability we need.
/// </summary>
internal sealed class AgendaEntryConfiguration : IEntityTypeConfiguration<AgendaEntry>
{
    public void Configure(EntityTypeBuilder<AgendaEntry> builder)
    {
        builder.ToTable("agenda_entries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.CompanyId).IsRequired();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(300);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.StartDateUtc).IsRequired();
        builder.Property(e => e.EndDateUtc).IsRequired();
        builder.Property(e => e.IsAllDay).IsRequired();
        builder.Property(e => e.ActivityType).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.ExperimentId);
        builder.Property(e => e.ResponsibleId).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();

        // Recurrence rule: persist the value object's canonical text; rebuild it through the validating
        // factory on read so a hand-edited/corrupt row surfaces immediately rather than flowing on silently.
        builder.Property(e => e.RecurrenceRule)
            .HasColumnName("recurrence_rule")
            .HasMaxLength(500)
            .HasConversion(
                spec => spec == null ? null : spec.Value,
                value => value == null ? null : RecurrenceRuleSpec.Create(value));

        // Excluded dates (RFC 5545 EXDATE): the private backing field is mapped as a single jsonb column of a
        // DateOnly array, compared element-wise so EF change tracking detects an added exclusion. Modelled as a
        // scalar (converted) property, not an EF collection navigation, because it is a small value-type set
        // always loaded with its parent and never queried on its own.
        builder.Property<List<DateOnly>>("_excludedDates")
            .HasColumnName("excluded_dates")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasConversion(
                dates => JsonSerializer.Serialize(dates, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<DateOnly>>(json, (JsonSerializerOptions?)null) ?? new List<DateOnly>(),
                new ValueComparer<List<DateOnly>>(
                    (a, b) => (a ?? new List<DateOnly>()).SequenceEqual(b ?? new List<DateOnly>()),
                    dates => dates.Aggregate(0, (hash, date) => HashCode.Combine(hash, date.GetHashCode())),
                    dates => dates.ToList()));

        builder.HasIndex(e => new { e.CompanyId, e.StartDateUtc })
            .HasDatabaseName("ix_agenda_entries_company_start");
        builder.HasIndex(e => new { e.CompanyId, e.ActivityType })
            .HasDatabaseName("ix_agenda_entries_company_activity");
    }
}
