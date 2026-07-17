import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  DisposeStockRequest,
  ItemCategoryOption,
  LocationSummaryItem,
  RecentMovementItem,
  RegisterConsumptionRequest,
  RegisterStockEntryRequest,
  RegisterStockItemRequest,
  RegisterStorageLocationRequest,
  StockItemFilters,
  StockItemListItem,
  StockMovementListItem,
  StockMovementsFilter,
  StorageLocationListItem,
  TransferStockRequest,
  UnitOption,
  UpdateStockItemRequest,
  UpdateStorageLocationRequest,
} from '@/modules/inventory/types';

/**
 * Inventory module query keys, namespaced under 'inventory' so mutations can invalidate the
 * stock-item list (any page/filter) without touching other modules. `list` is parameterized by
 * the active filters and page so switching filters keeps its own cache entry.
 */
export const inventoryKeys = {
  all: ['inventory'] as const,
  stockItems: () => [...inventoryKeys.all, 'stock-items'] as const,
  stockItemList: (filters: StockItemFilters, page: number) =>
    [...inventoryKeys.stockItems(), 'list', filters, page] as const,
  movements: (stockItemId: string, filters: StockMovementsFilter, page: number) =>
    [...inventoryKeys.stockItems(), 'movements', stockItemId, filters, page] as const,
  // Nested under stockItems() so the movement mutations, which invalidate that namespace, also refresh
  // the cross-item recent-activity panel after an entry/consumption/transfer/disposal.
  recentMovements: (top: number) =>
    [...inventoryKeys.stockItems(), 'recent-movements', top] as const,
  locations: () => [...inventoryKeys.all, 'locations'] as const,
  // The full management listing (card [E7] #112). Kept separate from locations() (the item-browser summary)
  // so the two caches are distinct; the write mutations invalidate both, since a create/edit/toggle changes
  // the sidebar summary and the create-form dropdown as well as the management table.
  managedLocations: () => [...inventoryKeys.all, 'managed-locations'] as const,
  categories: () => [...inventoryKeys.all, 'categories'] as const,
  units: () => [...inventoryKeys.all, 'units'] as const,
};

const PAGE_SIZE = 20;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/** Paginated, filterable stock items of the active company for the inventory table. */
export function useStockItems(filters: StockItemFilters, page: number) {
  return useQuery({
    queryKey: inventoryKeys.stockItemList(filters, page),
    queryFn: () =>
      api.get<PagedResult<StockItemListItem>>(Endpoints.inventory.stockItems.list, {
        storageLocationId: filters.storageLocationId || undefined,
        category: filters.category || undefined,
        search: filters.search || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });
}

/**
 * Paginated, filterable movement history (ledger) of a single stock item. Disabled until an item is
 * selected, so the Movements tab does not fetch with an empty id.
 */
export function useStockMovements(
  stockItemId: string | undefined,
  filters: StockMovementsFilter,
  page: number,
) {
  return useQuery({
    queryKey: inventoryKeys.movements(stockItemId ?? '', filters, page),
    queryFn: () =>
      api.get<PagedResult<StockMovementListItem>>(
        Endpoints.inventory.stockItems.movements(stockItemId as string),
        {
          type: filters.type || undefined,
          from: filters.from || undefined,
          to: filters.to || undefined,
          page,
          pageSize: PAGE_SIZE,
        },
      ),
    enabled: Boolean(stockItemId),
    staleTime: 30_000,
  });
}

/**
 * The active company's most recent stock movements across every item, for the inventory page's
 * "recent activity" panel. Cross-item (never pinned to one item), capped to `top` rows (backend clamps
 * it). Nested under the stock-items query namespace, so a movement mutation refreshes it automatically.
 */
export function useRecentMovements(top = 20) {
  return useQuery({
    queryKey: inventoryKeys.recentMovements(top),
    queryFn: () =>
      api.get<RecentMovementItem[]>(Endpoints.inventory.stockMovements.recent, { top }),
    staleTime: 30_000,
  });
}

/** Storage locations of the active company — feeds the location filter and transfer target. */
export function useStorageLocations() {
  return useQuery({
    queryKey: inventoryKeys.locations(),
    queryFn: () =>
      api.get<LocationSummaryItem[]>(Endpoints.inventory.storageLocations.summary),
    staleTime: 5 * 60_000,
  });
}

/**
 * Full storage-location listing for the "Gerenciar locais" management modal (card [E7] #112): every location
 * (active or not) with its editable metadata and derived item count. Distinct from {@link useStorageLocations}
 * (the item-browser sidebar/dropdown summary). `enabled` keeps it from fetching until the modal is opened.
 */
export function useManagedStorageLocations(enabled = true) {
  return useQuery({
    queryKey: inventoryKeys.managedLocations(),
    queryFn: () =>
      api.get<StorageLocationListItem[]>(Endpoints.inventory.storageLocations.root),
    staleTime: 60_000,
    enabled,
  });
}

/**
 * Per-tenant item categories for the create form and category filter. `enabled` keeps the
 * catalogue from being fetched until a form/filter that needs it is shown.
 */
export function useItemCategories(enabled = true) {
  return useQuery({
    queryKey: inventoryKeys.categories(),
    queryFn: () => api.get<ItemCategoryOption[]>(Endpoints.configuration.itemCategories),
    staleTime: 5 * 60_000,
    enabled,
  });
}

/** Per-tenant units of measure for the create/movement forms. */
export function useUnits(enabled = true) {
  return useQuery({
    queryKey: inventoryKeys.units(),
    queryFn: () => api.get<UnitOption[]>(Endpoints.configuration.units),
    staleTime: 5 * 60_000,
    enabled,
  });
}

// ---------------------------------------------------------------------------
// Mutations — each invalidates the whole stock-item list (all pages/filters).
// ---------------------------------------------------------------------------

/** Registers a new stock item with its initial balance; refreshes the item list. */
export function useRegisterStockItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterStockItemRequest) =>
      api.post<string>(Endpoints.inventory.stockItems.create, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() }),
  });
}

/**
 * Corrects an existing item's metadata (name, category, location, minimum, brand, application) via the PUT
 * endpoint; refreshes the item list so the table and detail sheet re-render with the new values. Never
 * touches the balance, lot or expiry — those change only through movements.
 */
export function useUpdateStockItem(stockItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateStockItemRequest) =>
      api.put<void>(Endpoints.inventory.stockItems.byId(stockItemId), body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() }),
  });
}

/** Registers an incoming entry (receipt) on an item; refreshes the item list. */
export function useRegisterEntry(stockItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterStockEntryRequest) =>
      api.post<string>(Endpoints.inventory.stockItems.entries(stockItemId), body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() }),
  });
}

/** Registers a consumption on an item; refreshes the item list. */
export function useRegisterConsumption(stockItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterConsumptionRequest) =>
      api.post<void>(Endpoints.inventory.stockItems.consumptions(stockItemId), body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() }),
  });
}

/** Transfers an item to another storage location; refreshes the item list. */
export function useTransferStock(stockItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: TransferStockRequest) =>
      api.post<void>(Endpoints.inventory.stockItems.transfers(stockItemId), body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() }),
  });
}

/** Disposes a quantity of an item (auditable); refreshes the item list. */
export function useDisposeStock(stockItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: DisposeStockRequest) =>
      api.post<void>(Endpoints.inventory.stockItems.disposals(stockItemId), body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() }),
  });
}

// ---------------------------------------------------------------------------
// Storage-location management mutations (card [E7] #112).
// Each refreshes both the management listing and the item-browser summary (sidebar + create-form dropdown),
// so a new/edited/toggled location shows up everywhere it is consumed without a manual refetch.
// ---------------------------------------------------------------------------

/** Invalidates every storage-location cache: the management listing and the summary sidebar/dropdown. */
function invalidateStorageLocations(queryClient: ReturnType<typeof useQueryClient>) {
  queryClient.invalidateQueries({ queryKey: inventoryKeys.managedLocations() });
  queryClient.invalidateQueries({ queryKey: inventoryKeys.locations() });
}

/** Registers a new storage location; refreshes the management list and the summary dropdown. */
export function useCreateStorageLocation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterStorageLocationRequest) =>
      api.post<string>(Endpoints.inventory.storageLocations.root, body),
    onSuccess: () => invalidateStorageLocations(queryClient),
  });
}

/** Corrects a storage location's metadata (never its type); refreshes both location caches. */
export function useUpdateStorageLocation(storageLocationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateStorageLocationRequest) =>
      api.put<void>(Endpoints.inventory.storageLocations.byId(storageLocationId), body),
    onSuccess: () => invalidateStorageLocations(queryClient),
  });
}

/** Activates/deactivates a storage location (preserving history); refreshes both location caches. */
export function useToggleStorageLocationStatus(storageLocationId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (isActive: boolean) =>
      api.patch<void>(Endpoints.inventory.storageLocations.status(storageLocationId), {
        isActive,
      }),
    onSuccess: () => invalidateStorageLocations(queryClient),
  });
}
