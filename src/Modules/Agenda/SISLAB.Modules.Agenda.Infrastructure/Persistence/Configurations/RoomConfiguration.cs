using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Agenda.Domain.Rooms;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.CompanyId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Capacity).IsRequired();
        builder.Property(r => r.Type).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.IsActive).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();

        builder.HasIndex(r => new { r.CompanyId, r.Id })
            .HasDatabaseName("ix_rooms_company_id_id");
    }
}

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.CompanyId).IsRequired();
        builder.Property(b => b.RoomId).IsRequired();
        builder.Property(b => b.BookedByName).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Activity).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(b => b.Date).IsRequired();
        builder.Property(b => b.StartTime).IsRequired();
        builder.Property(b => b.EndTime).IsRequired();
        builder.Property(b => b.Notes).HasMaxLength(1000);
        builder.Property(b => b.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.HasConflictWarning).IsRequired();
        builder.Property(b => b.CreatedAtUtc).IsRequired();

        builder.HasIndex(b => new { b.CompanyId, b.RoomId, b.Date })
            .HasDatabaseName("ix_bookings_company_room_date");
        builder.HasIndex(b => new { b.CompanyId, b.Date })
            .HasDatabaseName("ix_bookings_company_date");
    }
}
