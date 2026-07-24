using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Experiments.Domain.Collection;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="CollectionPlan"/> aggregate and its owned children (SISLAB-08). Tables under
/// the <c>experiments</c> schema: <c>collection_plans</c> → <c>collection_sample_routings</c> →
/// <c>collection_planned_analyses</c>, and <c>collection_plans</c> → <c>collection_role_assignments</c>.
/// </summary>
/// <remarks>
/// The routings and their planned analyses are owned collections (child tables), because the status board's read-side
/// joins each planned analysis by name to the biobank's real analyses — a queryable shape, not a JSON blob. Owned types
/// name their columns explicitly in snake_case because the base context skips the snake_case pass for owned types. A
/// tenant has one plan per batch, and a plan routes one row per sample type and assigns one member per role — each
/// enforced by a unique index.
/// </remarks>
internal sealed class CollectionPlanConfiguration : IEntityTypeConfiguration<CollectionPlan>
{
    public void Configure(EntityTypeBuilder<CollectionPlan> builder)
    {
        builder.ToTable("collection_plans");

        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).ValueGeneratedNever();

        builder.Property(plan => plan.CompanyId).IsRequired();
        builder.Property(plan => plan.ProjectId).IsRequired();
        builder.Property(plan => plan.BatchId).IsRequired();

        builder.OwnsMany(plan => plan.Routings, ConfigureRoutings);
        builder.Navigation(plan => plan.Routings).AutoInclude();

        builder.OwnsMany(plan => plan.Assignments, ConfigureAssignments);
        builder.Navigation(plan => plan.Assignments).AutoInclude();

        // Tenant isolation access path; one plan per batch.
        builder.HasIndex(plan => new { plan.CompanyId, plan.Id })
            .HasDatabaseName("ix_collection_plans_company_id_id");
        builder.HasIndex(plan => new { plan.CompanyId, plan.BatchId })
            .IsUnique()
            .HasDatabaseName("ux_collection_plans_company_id_batch_id");
    }

    private static void ConfigureRoutings(OwnedNavigationBuilder<CollectionPlan, SampleRouting> routings)
    {
        routings.ToTable("collection_sample_routings");

        routings.WithOwner().HasForeignKey("plan_id");
        routings.HasKey(routing => routing.Id);
        routings.Property(routing => routing.Id).HasColumnName("id").ValueGeneratedNever();
        routings.Property<Guid>("plan_id");

        routings.Property(routing => routing.SampleType)
            .HasColumnName("sample_type").HasConversion<string>().HasMaxLength(30).IsRequired();

        routings.Property(routing => routing.StorageRoomId).HasColumnName("storage_room_id");
        routings.Property(routing => routing.StorageLabel).HasColumnName("storage_label").HasMaxLength(120);

        // Optional conservation temperature range, table-split into nullable columns on the routing row.
        routings.OwnsOne(routing => routing.ConservationRange, range =>
        {
            range.Property(r => r.MinimumCelsius)
                .HasColumnName("conservation_temp_min").HasColumnType("numeric(6,2)");
            range.Property(r => r.MaximumCelsius)
                .HasColumnName("conservation_temp_max").HasColumnType("numeric(6,2)");
        });

        routings.OwnsMany(routing => routing.PlannedAnalyses, ConfigurePlannedAnalyses);
        routings.Navigation(routing => routing.PlannedAnalyses).AutoInclude();

        // One routing per sample type within a plan. The index mixes the shadow FK property "plan_id" with the CLR
        // property "SampleType" (referenced by its property name, not its column name).
        routings.HasIndex("plan_id", nameof(SampleRouting.SampleType))
            .IsUnique()
            .HasDatabaseName("ux_collection_sample_routings_plan_id_sample_type");
    }

    private static void ConfigurePlannedAnalyses(OwnedNavigationBuilder<SampleRouting, PlannedAnalysis> analyses)
    {
        analyses.ToTable("collection_planned_analyses");

        analyses.WithOwner().HasForeignKey("routing_id");
        analyses.HasKey(analysis => analysis.Id);
        analyses.Property(analysis => analysis.Id).HasColumnName("id").ValueGeneratedNever();
        analyses.Property<Guid>("routing_id");

        analyses.Property(analysis => analysis.Name).HasColumnName("name").HasMaxLength(200).IsRequired();

        analyses.HasIndex("routing_id").HasDatabaseName("ix_collection_planned_analyses_routing_id");
    }

    private static void ConfigureAssignments(
        OwnedNavigationBuilder<CollectionPlan, CollectionRoleAssignment> assignments)
    {
        assignments.ToTable("collection_role_assignments");

        assignments.WithOwner().HasForeignKey("plan_id");
        assignments.HasKey(assignment => assignment.Id);
        assignments.Property(assignment => assignment.Id).HasColumnName("id").ValueGeneratedNever();
        assignments.Property<Guid>("plan_id");

        assignments.Property(assignment => assignment.RoleId).HasColumnName("role_id").IsRequired();
        assignments.Property(assignment => assignment.UserId).HasColumnName("user_id").IsRequired();

        // One assignment per role within a plan. Mixes the shadow FK "plan_id" with the CLR property "RoleId".
        assignments.HasIndex("plan_id", nameof(CollectionRoleAssignment.RoleId))
            .IsUnique()
            .HasDatabaseName("ux_collection_role_assignments_plan_id_role_id");
    }
}
