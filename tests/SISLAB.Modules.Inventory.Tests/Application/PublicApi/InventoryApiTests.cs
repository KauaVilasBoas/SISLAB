using SISLAB.Modules.Inventory.Application.PublicApi;
using SISLAB.Modules.Inventory.Application.StockRead;
using SISLAB.Modules.Inventory.Contracts.Dtos;
using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Modules.Inventory.Tests.Application.PublicApi;

/// <summary>
/// Covers the <see cref="InventoryApi"/> adapter (card [E5] #35): the translation from the module's
/// internal read models (the E4/E5 query results) into the primitives-only Contracts DTOs, the null
/// semantics of the by-id operations, and the exhaustion walk that turns the paged E4 queries into the
/// unpaged listings the E6 alert jobs consume. The mediator is faked so the adapter is exercised without
/// a live database — the SQL bodies themselves are covered by the read-side query tests.
/// </summary>
public sealed class InventoryApiTests
{
    private static readonly Guid Company = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LocationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // --- GetStockItemAsync ---------------------------------------------------------------------------

    [Fact]
    public async Task GetStockItem_maps_every_field_of_the_detail_onto_the_summary_dto()
    {
        StockItemDetail detail = SampleDetail();
        var api = new InventoryApi(new FakeMediator { StockItemDetail = detail });

        StockItemSummaryDto? dto = await api.GetStockItemAsync(ItemId, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(detail.Id, dto!.Id);
        Assert.Equal(detail.Name, dto.Name);
        Assert.Equal(detail.Category, dto.Category);
        Assert.Equal(detail.Quantity, dto.QuantityValue);
        Assert.Equal(detail.Unit, dto.QuantityUnit);
        Assert.Equal(detail.MinimumQuantity, dto.MinimumQuantityValue);
        Assert.Equal(detail.MinimumUnit, dto.MinimumQuantityUnit);
        Assert.Equal(detail.ExpiryYear, dto.ExpiryYear);
        Assert.Equal(detail.ExpiryMonth, dto.ExpiryMonth);
        Assert.Equal(detail.StorageLocationId, dto.StorageLocationId);
        Assert.Equal(detail.StorageLocationName, dto.StorageLocationName);
        Assert.Equal(detail.IsControlled, dto.IsControlled);
        Assert.Equal(detail.CompanyId, dto.CompanyId);
    }

    [Fact]
    public async Task GetStockItem_returns_null_when_the_item_does_not_exist()
    {
        var api = new InventoryApi(new FakeMediator { StockItemDetail = null });

        StockItemSummaryDto? dto = await api.GetStockItemAsync(ItemId, CancellationToken.None);

        Assert.Null(dto);
    }

    [Fact]
    public async Task GetStockItem_preserves_a_null_validity()
    {
        StockItemDetail detail = SampleDetail() with { ExpiryYear = null, ExpiryMonth = null };
        var api = new InventoryApi(new FakeMediator { StockItemDetail = detail });

        StockItemSummaryDto? dto = await api.GetStockItemAsync(ItemId, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Null(dto!.ExpiryYear);
        Assert.Null(dto.ExpiryMonth);
    }

    // --- StockItemExistsAsync ------------------------------------------------------------------------

    [Fact]
    public async Task StockItemExists_is_true_when_the_query_returns_a_row()
    {
        var api = new InventoryApi(new FakeMediator { StockItemDetail = SampleDetail() });

        Assert.True(await api.StockItemExistsAsync(ItemId, CancellationToken.None));
    }

    [Fact]
    public async Task StockItemExists_is_false_when_the_query_returns_no_row()
    {
        var api = new InventoryApi(new FakeMediator { StockItemDetail = null });

        Assert.False(await api.StockItemExistsAsync(ItemId, CancellationToken.None));
    }

    // --- GetOnHandBalanceAsync -----------------------------------------------------------------------

    [Fact]
    public async Task GetOnHandBalance_maps_the_quantity_and_unit_onto_the_balance_dto()
    {
        StockItemDetail detail = SampleDetail();
        var api = new InventoryApi(new FakeMediator { StockItemDetail = detail });

        StockBalanceDto? dto = await api.GetOnHandBalanceAsync(ItemId, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(detail.Id, dto!.StockItemId);
        Assert.Equal(detail.Quantity, dto.QuantityValue);
        Assert.Equal(detail.Unit, dto.QuantityUnit);
    }

    [Fact]
    public async Task GetOnHandBalance_returns_null_when_the_item_does_not_exist()
    {
        var api = new InventoryApi(new FakeMediator { StockItemDetail = null });

        Assert.Null(await api.GetOnHandBalanceAsync(ItemId, CancellationToken.None));
    }

    // --- ListExpiringItemsAsync ----------------------------------------------------------------------

    [Fact]
    public async Task ListExpiringItems_maps_every_field_of_the_result_onto_the_dto()
    {
        ExpiringItem item = SampleExpiring();
        var mediator = new FakeMediator();
        mediator.ExpiringPages.Add(Page(new[] { item }, totalCount: 1, pageSize: 200));
        var api = new InventoryApi(mediator);

        IReadOnlyList<ExpiringItemDto> dtos = await api.ListExpiringItemsAsync(30, CancellationToken.None);

        ExpiringItemDto dto = Assert.Single(dtos);
        Assert.Equal(item.Id, dto.StockItemId);
        Assert.Equal(item.Name, dto.Name);
        Assert.Equal(item.ExpiryYear, dto.ExpiryYear);
        Assert.Equal(item.ExpiryMonth, dto.ExpiryMonth);
        Assert.Equal(item.StorageLocationId, dto.StorageLocationId);
        Assert.Equal(item.StorageLocationName, dto.StorageLocationName);
    }

    [Fact]
    public async Task ListExpiringItems_forwards_the_window_to_the_query()
    {
        var mediator = new FakeMediator();
        mediator.ExpiringPages.Add(Page(Array.Empty<ExpiringItem>(), totalCount: 0, pageSize: 200));
        var api = new InventoryApi(mediator);

        await api.ListExpiringItemsAsync(7, CancellationToken.None);

        var query = Assert.IsType<ListExpiringItemsQuery>(mediator.SentRequests[0]);
        Assert.Equal(7, query.WarningWindowDays);
        Assert.True(query.IncludeExpired);
    }

    [Fact]
    public async Task ListExpiringItems_drains_every_page_until_the_total_is_covered()
    {
        // 3 items across two pages of size 2 (TotalCount = 3 → TotalPages = 2). The adapter must issue
        // exactly two round-trips and return all three, without asking for a third (empty) page.
        var mediator = new FakeMediator();
        mediator.ExpiringPages.Add(Page(new[] { Expiring("A"), Expiring("B") }, totalCount: 3, pageSize: 2));
        mediator.ExpiringPages.Add(Page(new[] { Expiring("C") }, totalCount: 3, pageSize: 2));
        var api = new InventoryApi(mediator);

        IReadOnlyList<ExpiringItemDto> dtos = await api.ListExpiringItemsAsync(30, CancellationToken.None);

        Assert.Equal(new[] { "A", "B", "C" }, dtos.Select(d => d.Name));
        Assert.Equal(2, mediator.SentRequests.Count);
    }

    [Fact]
    public async Task ListExpiringItems_returns_empty_when_there_is_nothing_at_risk()
    {
        var mediator = new FakeMediator();
        mediator.ExpiringPages.Add(Page(Array.Empty<ExpiringItem>(), totalCount: 0, pageSize: 200));
        var api = new InventoryApi(mediator);

        Assert.Empty(await api.ListExpiringItemsAsync(30, CancellationToken.None));
        Assert.Single(mediator.SentRequests); // only the first page is fetched
    }

    // --- ListItemsBelowMinimumAsync ------------------------------------------------------------------

    [Fact]
    public async Task ListItemsBelowMinimum_maps_every_field_of_the_result_onto_the_dto()
    {
        BelowMinimumItem item = SampleBelowMinimum();
        var mediator = new FakeMediator();
        mediator.BelowMinimumPages.Add(Page(new[] { item }, totalCount: 1, pageSize: 200));
        var api = new InventoryApi(mediator);

        IReadOnlyList<BelowMinimumItemDto> dtos =
            await api.ListItemsBelowMinimumAsync(CancellationToken.None);

        BelowMinimumItemDto dto = Assert.Single(dtos);
        Assert.Equal(item.Id, dto.StockItemId);
        Assert.Equal(item.Name, dto.Name);
        Assert.Equal(item.Quantity, dto.CurrentQuantityValue);
        Assert.Equal(item.Unit, dto.CurrentQuantityUnit);
        Assert.Equal(item.MinimumQuantity, dto.MinimumQuantityValue);
        Assert.Equal(item.MinimumUnit, dto.MinimumQuantityUnit);
    }

    [Fact]
    public async Task ListItemsBelowMinimum_drains_every_page_until_the_total_is_covered()
    {
        var mediator = new FakeMediator();
        mediator.BelowMinimumPages.Add(Page(new[] { BelowMinimum("A"), BelowMinimum("B") }, totalCount: 3, pageSize: 2));
        mediator.BelowMinimumPages.Add(Page(new[] { BelowMinimum("C") }, totalCount: 3, pageSize: 2));
        var api = new InventoryApi(mediator);

        IReadOnlyList<BelowMinimumItemDto> dtos =
            await api.ListItemsBelowMinimumAsync(CancellationToken.None);

        Assert.Equal(new[] { "A", "B", "C" }, dtos.Select(d => d.Name));
        Assert.Equal(2, mediator.SentRequests.Count);
    }

    // --- Fixtures ------------------------------------------------------------------------------------

    private static StockItemDetail SampleDetail() => new(
        Id: ItemId,
        Name: "Ethanol",
        Category: "Solvent",
        Quantity: 12.5m,
        Unit: "mL",
        MinimumQuantity: 5m,
        MinimumUnit: "mL",
        ExpiryYear: 2027,
        ExpiryMonth: 3,
        StorageLocationId: LocationId,
        StorageLocationName: "Shelf A",
        IsControlled: true,
        CompanyId: Company);

    private static ExpiringItem SampleExpiring() => Expiring("Ethanol");

    private static ExpiringItem Expiring(string name) => new(
        Id: Guid.NewGuid(),
        Name: name,
        Category: "Solvent",
        LotCode: "L-1",
        Quantity: 1m,
        Unit: "mL",
        ExpiryYear: 2027,
        ExpiryMonth: 3,
        ExpiryStatus: ExpiryStatusView.ExpiringSoon,
        DaysRemaining: 10,
        IsControlled: false,
        StorageLocationId: LocationId,
        StorageLocationName: "Shelf A",
        StorageLocationType: "Shelf");

    private static BelowMinimumItem SampleBelowMinimum() => BelowMinimum("Ethanol");

    private static BelowMinimumItem BelowMinimum(string name) => new(
        Id: Guid.NewGuid(),
        Name: name,
        Category: "Solvent",
        Brand: "Acme",
        Quantity: 2m,
        Unit: "mL",
        MinimumQuantity: 5m,
        MinimumUnit: "mL",
        Deficit: 3m,
        IsControlled: false,
        StorageLocationId: LocationId,
        StorageLocationName: "Shelf A",
        StorageLocationType: "Shelf");

    private static PagedResult<T> Page<T>(IReadOnlyList<T> items, int totalCount, int pageSize) =>
        new(items, totalCount, page: 1, pageSize);

    /// <summary>
    /// Minimal <see cref="IMediator"/> that returns pre-seeded results by request type and records every
    /// dispatched request. The paged listings are served page by page from a queue, so the adapter's
    /// exhaustion walk can be asserted deterministically.
    /// </summary>
    private sealed class FakeMediator : IMediator
    {
        public StockItemDetail? StockItemDetail { get; set; }

        public List<PagedResult<ExpiringItem>> ExpiringPages { get; } = new();

        public List<PagedResult<BelowMinimumItem>> BelowMinimumPages { get; } = new();

        public List<object> SentRequests { get; } = new();

        private int _expiringCursor;
        private int _belowMinimumCursor;

        public Task<TResult> SendAsync<TResult>(
            IRequest<TResult> request,
            CancellationToken cancellationToken = default)
        {
            SentRequests.Add(request);

            return request switch
            {
                GetStockItemDetailQuery => Task.FromResult((TResult)(object)StockItemDetail!),
                ListExpiringItemsQuery => Task.FromResult((TResult)(object)ExpiringPages[_expiringCursor++]),
                ListItemsBelowMinimumQuery => Task.FromResult((TResult)(object)BelowMinimumPages[_belowMinimumCursor++]),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };
        }
    }
}
