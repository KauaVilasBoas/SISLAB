import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type { StockItemFilters, StockItemListItem } from '@/modules/inventory/types';

/**
 * Data layer for the QR-labels screen (card [E7] #92).
 *
 * Storage locations reuse the Inventory `useStorageLocations` hook verbatim — there is nothing to add
 * here. The one thing the labels flow needs that the inventory table does not is the *whole* filtered
 * item set at once (you print every matching sticker, not one page of twenty), so this module owns a
 * single-page, large-window query over the same `GET /api/inventory/stock-items` endpoint rather than
 * driving the paginated inventory listing.
 */

export const labelsKeys = {
  all: ['labels'] as const,
  items: (filters: StockItemFilters) => [...labelsKeys.all, 'items', filters] as const,
};

/**
 * Upper bound on stickers fetched in one go. A single lab's catalogue is well under this; the cap keeps
 * a pathological tenant from trying to render thousands of QR canvases (and printing a phone book). When
 * the total exceeds it, the UI tells the operator to narrow the filter.
 */
export const LABEL_ITEMS_LIMIT = 500;

/** The full, filterable stock-item set of the active company to turn into item labels (up to the cap). */
export function useLabelStockItems(filters: StockItemFilters) {
  return useQuery({
    queryKey: labelsKeys.items(filters),
    queryFn: () =>
      api.get<PagedResult<StockItemListItem>>(Endpoints.inventory.stockItems.list, {
        storageLocationId: filters.storageLocationId || undefined,
        category: filters.category || undefined,
        search: filters.search || undefined,
        page: 1,
        pageSize: LABEL_ITEMS_LIMIT,
      }),
    staleTime: 30_000,
  });
}
