using Microsoft.EntityFrameworkCore;

namespace SISLAB.Infrastructure.Outbox;

/// <summary>
/// Interface de acesso ao DbSet de Outbox. Implementada por cada <see cref="SISLAB.Infrastructure.Persistence.SislabDbContextBase"/>
/// derivado que participa do Transactional Outbox Pattern.
///
/// COMO IMPLEMENTAR:
/// Cada DbContext de módulo que precisa do Outbox deve:
/// 1. Implementar esta interface.
/// 2. Declarar: public DbSet&lt;OutboxMessage&gt; OutboxMessages => Set&lt;OutboxMessage&gt;();
/// 3. Registrar a configuração <see cref="OutboxMessageConfiguration"/> no OnModelCreating.
/// </summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }
}
