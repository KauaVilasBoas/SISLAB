using Microsoft.EntityFrameworkCore;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Outbox DbSet accessor. Implemented by each <see cref="SISLAB.Infrastructure.Persistence.SislabDbContextBase"/>
/// derived context that participates in the Transactional Outbox Pattern.
///
/// To opt in, a module DbContext must:
/// 1. Implement this interface.
/// 2. Expose: public DbSet&lt;OutboxMessage&gt; OutboxMessages => Set&lt;OutboxMessage&gt;();
/// 3. Register <see cref="OutboxMessageConfiguration"/> in OnModelCreating.
/// </summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
}
