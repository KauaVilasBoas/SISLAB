using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.Rooms;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Room"/> aggregate (schema <c>configuration</c>). The room name is
/// unique per tenant.
/// </summary>
internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");

        builder.HasKey(room => room.Id);
        builder.Property(room => room.Id).ValueGeneratedNever();

        builder.Property(room => room.CompanyId).IsRequired();

        builder.Property(room => room.Name)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(room => room.RequiresAuthorization).IsRequired();

        // Room name is unique per tenant.
        builder.HasIndex(room => new { room.CompanyId, room.Name })
            .IsUnique()
            .HasDatabaseName("ux_rooms_company_id_name");

        // Tenant isolation access path.
        builder.HasIndex(room => new { room.CompanyId, room.Id })
            .HasDatabaseName("ix_rooms_company_id_id");
    }
}
