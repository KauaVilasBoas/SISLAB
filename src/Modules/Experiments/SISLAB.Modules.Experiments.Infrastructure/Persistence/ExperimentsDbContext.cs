using Microsoft.EntityFrameworkCore;
using SISLAB.Infrastructure.Multitenancy;
using SISLAB.Infrastructure.Outbox;
using SISLAB.Infrastructure.Persistence;
using SISLAB.Modules.Experiments.Domain.Attachments;
using SISLAB.Modules.Experiments.Domain.Biobank;
using SISLAB.Modules.Experiments.Domain.Collection;
using SISLAB.Modules.Experiments.Domain.Experiments;
using SISLAB.Modules.Experiments.Domain.Projects;
using SISLAB.Modules.Experiments.Infrastructure.Persistence.Configurations;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence;

/// <summary>
/// DbContext for the Experiments module (write-side, decision card #68). Manages the <see cref="Experiment"/>
/// TPH hierarchy (the <see cref="PlateExperiment"/> assays — viability and nitric oxide) with its owned steps,
/// plate wells and calculation-result snapshot, in the <c>experiments</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Experiment"/> is an <see cref="ITenantEntity"/>, so the context is built with the tenant services
/// resolved from DI; the base (<see cref="SislabDbContextBase"/>) applies the global query filter by
/// <c>company_id</c> and installs the tenant-stamping save interceptor. At design time (migrations) the tenant
/// services are absent and the filter is skipped — correct for schema generation.
/// </para>
/// <para>
/// It participates in the Transactional Outbox (<see cref="IOutboxDbContext"/>): the aggregate raises
/// <c>ExperimentCalculatedEvent</c>, which <see cref="EfUnitOfWork{TContext}"/> translates to the public
/// integration event and writes to <c>outbox_messages</c> in the same transaction as the aggregate change, so
/// the Inventory module can correlate reagent consumption to a calculated experiment (card #109).
/// </para>
/// </remarks>
public sealed class ExperimentsDbContext : SislabDbContextBase, IOutboxDbContext
{
    public ExperimentsDbContext(
        DbContextOptions<ExperimentsDbContext> options,
        ITenantContext? tenantContext = null,
        ITenantBypass? tenantBypass = null)
        : base(options, tenantContext, tenantBypass) { }

    public DbSet<Experiment> Experiments => Set<Experiment>();

    /// <summary>In vivo experimental designs (decision card [E11] #73): Project → Batch → Group → Animal.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>Biobank samples (decision card [E11] #89, F4): Sample → Analysis, with a derived balance.</summary>
    public DbSet<Sample> Samples => Set<Sample>();

    /// <summary>Collection plans (SISLAB-08): CollectionPlan → SampleRouting → PlannedAnalysis, + role assignments.</summary>
    public DbSet<CollectionPlan> CollectionPlans => Set<CollectionPlan>();

    /// <summary>Evidence attachments (SISLAB-09): a file's storage key + metadata, linked to an animal's reading/analysis.</summary>
    public DbSet<Attachment> Attachments => Set<Attachment>();

    /// <inheritdoc />
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Every table of this module (aggregate hierarchy + outbox) lives in the "experiments" schema.
        modelBuilder.HasDefaultSchema("experiments");

        modelBuilder.ApplyConfiguration(new ExperimentConfiguration());
        modelBuilder.ApplyConfiguration(new PlateExperimentConfiguration());
        modelBuilder.ApplyConfiguration(new BehavioralExperimentConfiguration());
        modelBuilder.ApplyConfiguration(new ProjectConfiguration());
        modelBuilder.ApplyConfiguration(new SampleConfiguration());
        modelBuilder.ApplyConfiguration(new CollectionPlanConfiguration());
        modelBuilder.ApplyConfiguration(new AttachmentConfiguration());

        // Outbox table lives in the module schema so the aggregate write and the outbox write share one
        // transaction/one connection (local transactional consistency).
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());

        // snake_case naming + tenant query filter applied by the base AFTER the configurations.
        base.OnModelCreating(modelBuilder);
    }
}
