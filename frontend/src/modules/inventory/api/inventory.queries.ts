import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  DisposeStockRequest,
  ItemCategoryOption,
  LocationSummaryItem,
  RegisterConsumptionRequest,
  RegisterStockEntryRequest,
  RegisterStockItemRequest,
  StockItemFilters,
  StockItemListItem,
  StockMovementListItem,
  StockMovementsFilter,
  TransferStockRequest,
  UnitOption,
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
  locations: () => [...inventoryKeys.all, 'locations'] as const,
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
