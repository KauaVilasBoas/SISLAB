using System.Data;
using Dapper;
using SISLAB.Infrastructure.Data;

namespace SISLAB.Modules.Inventory.Infrastructure.ReadModels;

/// <summary>
/// Dapper/Npgsql implementation of <see cref="IStockMovementStore"/>. Writes into
/// <c>inventory.stock_movements</c> using the read-side connection factory (the read model is not managed
/// by the write DbContext). The insert is <c>ON CONFLICT (id) DO NOTHING</c>: the row's primary key is the
/// originating event id, so reprocessing the same Outbox event never duplicates a movement.
/// </summary>
internal sealed class StockMovementStore : IStockMovementStore
{
    private const string InsertMovementSql =
        """
        INSERT INTO inventory.stock_movements (
            id, company_id, stock_item_id, movement_type,
            quantity_amount, quantity_unit, occurred_on,
            experiment_id, partner_id, performed_by, created_at_utc)
        VALUES (
            @Id, @CompanyId, @StockItemId, @MovementType,
            @QuantityAmount, @QuantityUnit, @OccurredOn,
            @ExperimentId, @PartnerId, @PerformedBy, @CreatedAtUtc)
        ON CONFLICT (id) DO NOTHING;
        """;

    private readonly DbConnectionFactory _connectionFactory;

    public StockMovementStore(DbConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task AppendAsync(StockMovementRow row, CancellationToken cancellationToken = default)
    {
        using IDbConnection connection = await _connectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(new CommandDefinition(
            InsertMovementSql,
            new
            {
                row.Id,
                row.CompanyId,
                row.StockItemId,
                row.MovementType,
                row.QuantityAmount,
                row.QuantityUnit,
                row.OccurredOn,
                row.ExperimentId,
                row.PartnerId,
                row.PerformedBy,
                row.CreatedAtUtc
            },
            cancellationToken: cancellationToken));
    }
}
