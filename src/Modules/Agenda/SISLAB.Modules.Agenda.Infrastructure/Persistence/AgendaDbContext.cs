using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Agenda.Domain.Bioterium;
using SISLAB.Modules.Agenda.Domain.Presentations;
using SISLAB.Modules.Agenda.Domain.Rooms;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Agenda.Infrastructure.Persistence;

/// <summary>
/// Write-side DbContext for the Agenda module (card [E10] #67). Inherits the shared base for snake_case
/// naming and the tenant query filter. The three aggregates — Room/Booking, BioteriumAssignment,
/// Presentation — all land in the <c>agenda</c> PostgreSQL schema.
/// </summary>
public sealed class AgendaDbContext : SislabDbContextBase, IOutboxDbContext
{
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BioteriumAssignment> BioteriumAssignments => Set<BioteriumAssignment>();
    public DbSet<Presentation> Presentations => Set<Presentation>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public AgendaDbContext(
        DbContextOptions<AgendaDbContext> options,
        ITenantContext? tenantContext = null,
        ITenantBypass? tenantBypass = null)
        : base(options, tenantContext, tenantBypass) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("agenda");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgendaDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>().ToTable("outbox_messages");
    }
}
