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
/// <c>experiments</c> schema: <c>projects</c> → <c>project_batches</c> → <c>project_groups</c> →
/// <c>project_animals</c>. Owned entity types name their columns explicitly in snake_case because the base
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

        batches.OwnsMany(batch => batch.Groups, ConfigureGroups);
        batches.Navigation(batch => batch.Groups).AutoInclude();

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

        groups.OwnsMany(group => group.Animals, ConfigureAnimals);
        groups.Navigation(group => group.Animals).AutoInclude();

        groups.HasIndex("batch_id").HasDatabaseName("ix_project_groups_batch_id");
    }

    private static void ConfigureAnimals(OwnedNavigationBuilder<Group, Animal> animals)
    {
        animals.ToTable("project_animals");

        animals.WithOwner().HasForeignKey("group_id");
        animals.HasKey(animal => animal.Id);
        animals.Property(animal => animal.Id).HasColumnName("id").ValueGeneratedNever();
        animals.Property<Guid>("group_id");

        animals.Property(animal => animal.Identifier).HasColumnName("identifier").HasMaxLength(60).IsRequired();
        animals.Property(animal => animal.Sex)
            .HasColumnName("sex")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        animals.Property(animal => animal.WeightGrams)
            .HasColumnName("weight_grams")
            .HasColumnType("numeric(10,2)");

        animals.HasIndex("group_id").HasDatabaseName("ix_project_animals_group_id");
    }
}
