using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="StockItem"/> aggregate.
/// </summary>
/// <remarks>
/// Value-object mapping decisions (card [E3] #25):
/// <list type="bullet">
///   <item><see cref="Quantity"/> (on-hand and minimum) → owned type, one column for the decimal
///   amount and one for the unit symbol. Owned types keep the VO's structural mapping without a
///   separate table and let EF materialize it through its private constructor.</item>
///   <item><see cref="UnitOfMeasure"/> inside a quantity → stored by its symbol via a value
///   converter (the symbol round-trips through <see cref="UnitOfMeasure.FromSymbol"/>).</item>
///   <item><see cref="Lot"/> → a single nullable <c>lot_code</c> column via a value converter; a null
///   column reconstitutes as a null <see cref="Lot"/> (non-lot-controlled item).</item>
///   <item><see cref="ExpiryDate"/> → owned type with nullable <c>expiry_year</c> / <c>expiry_month</c>;
///   both null when the item has no validity.</item>
/// </list>
/// </remarks>
internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).ValueGeneratedNever();

        builder.Property(item => item.CompanyId)
            .IsRequired();

        builder.Property(item => item.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(item => item.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(item => item.Brand)
            .HasMaxLength(120);

        builder.Property(item => item.ContainerState)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.Application)
            .HasMaxLength(500);

        builder.Property(item => item.IsControlled)
            .IsRequired();

        // Storage location referenced by value — no cross-aggregate FK/navigation.
        builder.Property(item => item.StorageLocationId)
            .IsRequired();

        // On-hand quantity — a required VO mapped as a complex type (EF Core 8): columns on the same
        // table, no surrogate key/identity. Complex types are the natural fit for a value object and
        // avoid the owned-type key collision when the same CLR type (Quantity) appears twice.
        builder.ComplexProperty(item => item.Quantity, quantity =>
        {
            quantity.Property(q => q.Value)
                .HasColumnName("quantity_amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            quantity.Property(q => q.Unit)
                .HasColumnName("quantity_unit")
                .HasConversion(UnitOfMeasureConverter)
                .HasMaxLength(20)
                .IsRequired();
        });

        // Reorder threshold — same unit as the on-hand quantity (guarded by the aggregate).
        builder.ComplexProperty(item => item.MinimumQuantity, minimum =>
        {
            minimum.Property(q => q.Value)
                .HasColumnName("minimum_quantity_amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            minimum.Property(q => q.Unit)
                .HasColumnName("minimum_quantity_unit")
                .HasConversion(UnitOfMeasureConverter)
                .HasMaxLength(20)
                .IsRequired();
        });

        // Lot: nullable single column. Null column => null Lot (non-lot-controlled item).
        builder.Property(item => item.Lot)
            .HasColumnName("lot_code")
            .HasConversion(
                lot => lot == null ? null : lot.Code,
                code => Lot.FromCode(code))
            .HasMaxLength(64);

        // Expiry: month-granularity validity as two nullable columns.
        builder.OwnsOne(item => item.Expiry, expiry =>
        {
            expiry.Property(e => e.Year)
                .HasColumnName("expiry_year");

            expiry.Property(e => e.Month)
                .HasColumnName("expiry_month");
        });

        // Tenant isolation: index by (company_id, id). company_id NOT NULL is enforced above.
        builder.HasIndex(item => new { item.CompanyId, item.Id })
            .HasDatabaseName("ix_stock_items_company_id_id");
    }

    /// <summary>
    /// Converts a <see cref="UnitOfMeasure"/> to/from its symbol. The symbol is the VO's identity
    /// and round-trips through the validated factory, so an invalid persisted symbol fails fast.
    /// </summary>
    private static readonly Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<UnitOfMeasure, string>
        UnitOfMeasureConverter = new(
            unit => unit.Symbol,
            symbol => UnitOfMeasure.FromSymbol(symbol));
}
