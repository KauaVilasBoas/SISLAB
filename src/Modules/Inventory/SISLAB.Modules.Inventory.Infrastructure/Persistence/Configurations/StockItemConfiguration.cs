using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SISLAB.Modules.Inventory.Domain.StockItems;
using SISLAB.Modules.Inventory.Domain.ValueObjects;
using SISLAB.SharedKernel.Multitenancy;

namespace SISLAB.Modules.Inventory.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="StockItem"/> aggregate and its owned <see cref="StockBatch"/>
/// children.
/// </summary>
/// <remarks>
/// Value-object / batch mapping decisions (cards [E3] #25, [E4] #109):
/// <list type="bullet">
///   <item>The on-hand <see cref="StockItem.Quantity"/> is <b>derived</b> from the batches (a computed
///   property with no setter), so it is <b>not</b> a mapped column: the source of truth is the batch ledger
///   in <c>inventory.stock_batches</c>. The read-side <c>stock_view</c> sums the batches instead.</item>
///   <item>The item's fixed <see cref="StockItem.Unit"/> is persisted as a single <c>unit</c> column (the
///   minimum shares it), stored by its symbol via a value converter.</item>
///   <item><see cref="StockItem.MinimumQuantity"/> → owned type: one decimal column for the amount (the unit
///   is the shared item unit, so it is not duplicated).</item>
///   <item><see cref="StockBatch"/> → an owned collection in its own table <c>stock_batches</c>: each batch
///   carries its remaining/initial quantity, lot, month-granularity expiry, unit cost and receipt metadata.
///   Owned so the aggregate stays the only writer and EF loads the batches with the item automatically.</item>
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

        // Category referenced by value (card [E12] #76): a per-tenant Configuration category id. No
        // cross-module FK/navigation — the id is stored as a plain uuid, validated on the write-side command
        // via ILabConfiguration, exactly like storage_location_id.
        builder.Property(item => item.CategoryId)
            .IsRequired();

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

        // The item's fixed unit of measure (shared by the balance, the minimum and every batch). Stored by
        // symbol via a value converter; the balance itself is derived from the batches, not stored here.
        builder.Property(item => item.Unit)
            .HasColumnName("unit")
            .HasConversion(UnitOfMeasureConverter)
            .HasMaxLength(20)
            .IsRequired();

        // The on-hand Quantity is derived from the batches (computed, no setter) — tell EF to ignore it.
        builder.Ignore(item => item.Quantity);

        // Reorder threshold — a complex type carrying only the decimal amount; the unit is the item's `unit`.
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

        // Batches — an owned collection in its own table. Each batch is a child entity of the aggregate.
        builder.OwnsMany(item => item.Batches, ConfigureBatches);

        // Tenant isolation: index by (company_id, id). company_id NOT NULL is enforced above.
        builder.HasIndex(item => new { item.CompanyId, item.Id })
            .HasDatabaseName("ix_stock_items_company_id_id");
    }

    private static void ConfigureBatches(OwnedNavigationBuilder<StockItem, StockBatch> batches)
    {
        batches.ToTable("stock_batches");

        // The batch id is the primary key; the owning stock_item_id is the shadow FK EF adds for the owner.
        batches.WithOwner().HasForeignKey("stock_item_id");
        batches.HasKey(batch => batch.Id);
        batches.Property(batch => batch.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        batches.Property<Guid>("stock_item_id");

        // Remaining and initial balances — owned value objects (amount + unit). Complex types are not
        // available inside an owned collection in EF 8, so each quantity is an owned type carrying both
        // columns; the unit always equals the owning item's `unit` (guarded by the aggregate).
        batches.OwnsOne(batch => batch.RemainingQuantity, quantity =>
        {
            quantity.Property(q => q.Value)
                .HasColumnName("remaining_quantity_amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            quantity.Property(q => q.Unit)
                .HasColumnName("remaining_quantity_unit")
                .HasConversion(UnitOfMeasureConverter)
                .HasMaxLength(20)
                .IsRequired();
        });
        batches.Navigation(batch => batch.RemainingQuantity).IsRequired();

        batches.OwnsOne(batch => batch.InitialQuantity, quantity =>
        {
            quantity.Property(q => q.Value)
                .HasColumnName("initial_quantity_amount")
                .HasColumnType("numeric(18,4)")
                .IsRequired();

            quantity.Property(q => q.Unit)
                .HasColumnName("initial_quantity_unit")
                .HasConversion(UnitOfMeasureConverter)
                .HasMaxLength(20)
                .IsRequired();
        });
        batches.Navigation(batch => batch.InitialQuantity).IsRequired();

        // Lot: nullable single column. Null => null Lot (non-lot-controlled batch).
        batches.Property(batch => batch.Lot)
            .HasColumnName("lot_code")
            .HasConversion(
                lot => lot == null ? null : lot.Code,
                code => Lot.FromCode(code))
            .HasMaxLength(64);

        // Expiry: month-granularity validity as two nullable columns.
        batches.OwnsOne(batch => batch.Expiry, expiry =>
        {
            expiry.Property(e => e.Year).HasColumnName("expiry_year");
            expiry.Property(e => e.Month).HasColumnName("expiry_month");
        });

        batches.Property(batch => batch.UnitCostBrl)
            .HasColumnName("unit_cost_brl")
            .HasColumnType("numeric(12,4)");

        batches.Property(batch => batch.ReceivedAtUtc)
            .HasColumnName("received_at_utc")
            .IsRequired();

        batches.Property(batch => batch.SupplierPartnerId)
            .HasColumnName("supplier_partner_id");

        // Access path for FEFO reads by item (the batches of an item, ordered by expiry).
        batches.HasIndex("stock_item_id")
            .HasDatabaseName("ix_stock_batches_stock_item_id");
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
