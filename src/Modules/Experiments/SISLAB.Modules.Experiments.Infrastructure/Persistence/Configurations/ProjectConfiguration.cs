using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Experiments.Domain.Projects;

namespace SISLAB.Modules.Experiments.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="Project"/> aggregate and its owned batch/group/animal tree
/// (decision card [E11] #73, discovery decision F1 — the in vivo delineation).
/// </summary>
/// <remarks>
/// The whole delineation is one aggregate persisted as nested owned collections, each in its own table under the
/// <c>experiments</c> schema: <c>projects</c> → <c>project_batches</c> → {<c>project_groups</c>, <c>project_cages</c>
/// → <c>project_animals</c>}. Since SISLAB-03 the animal is owned by the <b>cage</b> (its physical parent), not the
/// group; the treatment group is an optional value reference on the animal (<c>group_id</c>, nullable, no FK — it is
/// resolved within the aggregate). Owned entity types name their columns explicitly in snake_case because the base
/// context skips the snake_case pass for owned types. The <see cref="Dose"/> value object is table-split into the
/// group row (amount + unit columns).
/// </remarks>
internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(project => project.Id);
        builder.Property(project => project.Id).ValueGeneratedNever();

        builder.Property(project => project.CompanyId).IsRequired();

        builder.Property(project => project.Name).IsRequired().HasMaxLength(200);
        builder.Property(project => project.Species).IsRequired().HasMaxLength(120);
        builder.Property(project => project.Description).HasMaxLength(2000);

        builder.Property(project => project.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(project => project.CurrentDesignVersion).IsRequired();

        builder.OwnsMany(project => project.Batches, ConfigureBatches);
        builder.Navigation(project => project.Batches).AutoInclude();

        builder.OwnsMany(project => project.PhysiologicalReadings, ConfigureReadings);
        builder.Navigation(project => project.PhysiologicalReadings).AutoInclude();

        builder.HasIndex(project => new { project.CompanyId, project.Id })
            .HasDatabaseName("ix_projects_company_id_id");
    }

    private static void ConfigureBatches(OwnedNavigationBuilder<Project, Batch> batches)
    {
        batches.ToTable("project_batches");

        batches.WithOwner().HasForeignKey("project_id");
        batches.HasKey(batch => batch.Id);
        batches.Property(batch => batch.Id).HasColumnName("id").ValueGeneratedNever();
        batches.Property<Guid>("project_id");

        batches.Property(batch => batch.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        batches.Property(batch => batch.DesignVersion).HasColumnName("design_version").IsRequired();
        batches.Property(batch => batch.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // The experimental model (SISLAB-04) the batch runs, held by value — the id of a Configuration
        // ExperimentalModel. Nullable: a batch may be planned before its model is chosen. No cross-module FK
        // (the referenced aggregate lives in another bounded context); it is a plain uuid column.
        batches.Property(batch => batch.ExperimentalModelId).HasColumnName("experimental_model_id");

        batches.OwnsMany(batch => batch.Groups, ConfigureGroups);
        batches.Navigation(batch => batch.Groups).AutoInclude();

        batches.OwnsMany(batch => batch.Cages, ConfigureCages);
        batches.Navigation(batch => batch.Cages).AutoInclude();

        // Batch.Animals is a read-only projection over the cages' animals (not a stored navigation) — ignore it so EF
        // does not try to map animals a second time under the batch.
        batches.Ignore(batch => batch.Animals);

        batches.HasIndex("project_id").HasDatabaseName("ix_project_batches_project_id");
    }

    private static void ConfigureGroups(OwnedNavigationBuilder<Batch, Group> groups)
    {
        groups.ToTable("project_groups");

        groups.WithOwner().HasForeignKey("batch_id");
        groups.HasKey(group => group.Id);
        groups.Property(group => group.Id).HasColumnName("id").ValueGeneratedNever();
        groups.Property<Guid>("batch_id");

        groups.Property(group => group.Name).HasColumnName("name").HasMaxLength(120).IsRequired();

        // Dose value object, table-split into the group row.
        groups.OwnsOne(group => group.Dose, dose =>
        {
            dose.Property(d => d.Amount).HasColumnName("dose_amount").HasColumnType("numeric(18,4)").IsRequired();
            dose.Property(d => d.Unit).HasColumnName("dose_unit").HasMaxLength(30).IsRequired();
        });
        groups.Navigation(group => group.Dose).IsRequired();

        groups.HasIndex("batch_id").HasDatabaseName("ix_project_groups_batch_id");
    }

    private static void ConfigureCages(OwnedNavigationBuilder<Batch, Cage> cages)
    {
        cages.ToTable("project_cages");

        cages.WithOwner().HasForeignKey("batch_id");
        cages.HasKey(cage => cage.Id);
        cages.Property(cage => cage.Id).HasColumnName("id").ValueGeneratedNever();
        cages.Property<Guid>("batch_id");

        cages.Property(cage => cage.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        cages.Property(cage => cage.Capacity).HasColumnName("capacity");

        cages.OwnsMany(cage => cage.Animals, ConfigureAnimals);
        cages.Navigation(cage => cage.Animals).AutoInclude();

        cages.HasIndex("batch_id").HasDatabaseName("ix_project_cages_batch_id");
    }

    private static void ConfigureAnimals(OwnedNavigationBuilder<Cage, Animal> animals)
    {
        animals.ToTable("project_animals");

        animals.WithOwner().HasForeignKey("cage_id");
        animals.HasKey(animal => animal.Id);
        animals.Property(animal => animal.Id).HasColumnName("id").ValueGeneratedNever();
        animals.Property<Guid>("cage_id");

        // The treatment group the animal is assigned to (SISLAB-03), held by value — nullable while unassigned. No FK:
        // the group is a sibling owned entity of the same batch, resolved within the aggregate (ids-by-value rule).
        animals.Property(animal => animal.GroupId).HasColumnName("group_id");

        animals.Property(animal => animal.Identifier).HasColumnName("identifier").HasMaxLength(60).IsRequired();
        animals.Property(animal => animal.Sex)
            .HasColumnName("sex")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        animals.Property(animal => animal.WeightGrams)
            .HasColumnName("weight_grams")
            .HasColumnType("numeric(10,2)");

        // Inclusion decision (SISLAB-02): an optional owned single table-split onto the animal row. All columns are
        // nullable together — an animal with no applied criterion carries no decision — and the value object is
        // rebuilt through its validated factory on read-back.
        animals.OwnsOne(animal => animal.Inclusion, inclusion =>
        {
            inclusion.Property(decision => decision.Status)
                .HasColumnName("inclusion_status")
                .HasConversion<string>()
                .HasMaxLength(20);
            inclusion.Property(decision => decision.ParameterCode)
                .HasColumnName("inclusion_parameter_code")
                .HasMaxLength(60);
            inclusion.Property(decision => decision.DecidingValue)
                .HasColumnName("inclusion_deciding_value")
                .HasColumnType("numeric(18,4)");
            inclusion.Property(decision => decision.Reason)
                .HasColumnName("inclusion_reason")
                .HasMaxLength(300);
        });

        animals.HasIndex("cage_id").HasDatabaseName("ix_project_animals_cage_id");
        animals.HasIndex(animal => animal.GroupId).HasDatabaseName("ix_project_animals_group_id");
    }

    private static void ConfigureReadings(OwnedNavigationBuilder<Project, PhysiologicalReading> readings)
    {
        readings.ToTable("project_physiological_readings");

        readings.WithOwner().HasForeignKey("project_id");
        readings.HasKey(reading => reading.Id);
        readings.Property(reading => reading.Id).HasColumnName("id").ValueGeneratedNever();
        readings.Property<Guid>("project_id");

        // The animal the reading is for, held by value (a project animal id) — no FK: the animal is an owned entity
        // of a group, and this is the module's ids-by-value rule applied within the same aggregate.
        readings.Property(reading => reading.AnimalId).HasColumnName("animal_id").IsRequired();

        readings.Property(reading => reading.ParameterCode)
            .HasColumnName("parameter_code").HasMaxLength(60).IsRequired();
        readings.Property(reading => reading.Value)
            .HasColumnName("value").HasColumnType("numeric(18,4)").IsRequired();
        readings.Property(reading => reading.Unit)
            .HasColumnName("unit").HasMaxLength(30).IsRequired();
        readings.Property(reading => reading.TimepointLabel)
            .HasColumnName("timepoint_label").HasMaxLength(60).IsRequired();
        readings.Property(reading => reading.RecordedBy)
            .HasColumnName("recorded_by").HasMaxLength(200).IsRequired();
        readings.Property(reading => reading.RecordedAtUtc)
            .HasColumnName("recorded_at_utc").IsRequired();

        readings.HasIndex("project_id").HasDatabaseName("ix_project_physiological_readings_project_id");
        readings.HasIndex(reading => reading.AnimalId)
            .HasDatabaseName("ix_project_physiological_readings_animal_id");
    }
}
