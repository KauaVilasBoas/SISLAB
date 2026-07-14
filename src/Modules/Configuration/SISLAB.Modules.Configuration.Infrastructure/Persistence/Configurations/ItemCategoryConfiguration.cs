using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Configuration.Domain.ItemCategories;

namespace SISLAB.Modules.Configuration.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ItemCategory"/> aggregate (schema <c>configuration</c>).
/// </summary>
/// <remarks>
/// <b>Aliases → one column.</b> <see cref="CategoryAliases"/> is a single conceptual value (a normalized set
/// of names), so it is flattened onto the row as a newline-joined <c>aliases</c> text column via a value
/// converter over the validated factory, rather than a child table. The category name is unique per tenant.
/// </remarks>
internal sealed class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    /// <summary>Separator for the alias set persisted as a single text column (a newline never appears in an alias).</summary>
    private const char AliasSeparator = '\n';

    public void Configure(EntityTypeBuilder<ItemCategory> builder)
    {
        builder.ToTable("item_categories");

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Id).ValueGeneratedNever();

        builder.Property(category => category.CompanyId).IsRequired();

        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(120);

        // Aliases value object: persisted as a single joined text column, reconstituted through the validated
        // CategoryAliases.From factory so an invalid persisted set fails fast on read-back.
        builder.Property(category => category.Aliases)
            .HasColumnName("aliases")
            .IsRequired()
            .HasConversion(
                aliases => string.Join(AliasSeparator, aliases.Values),
                value => CategoryAliases.From(SplitAliases(value)));

        builder.Property(category => category.IsControlled).IsRequired();

        // Category name is unique per tenant so imports/UI can resolve by name unambiguously.
        builder.HasIndex(category => new { category.CompanyId, category.Name })
            .IsUnique()
            .HasDatabaseName("ux_item_categories_company_id_name");

        // Tenant isolation access path.
        builder.HasIndex(category => new { category.CompanyId, category.Id })
            .HasDatabaseName("ix_item_categories_company_id_id");
    }

    private static IEnumerable<string> SplitAliases(string value)
        => string.IsNullOrEmpty(value)
            ? []
            : value.Split(AliasSeparator, StringSplitOptions.RemoveEmptyEntries);
}
