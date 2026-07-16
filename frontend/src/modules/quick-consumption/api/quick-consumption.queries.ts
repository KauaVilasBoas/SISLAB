import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { StockItemDetail } from '@/modules/quick-consumption/types';

/**
 * Quick-consumption query keys, namespaced under 'quick-consumption' so a scan's item-detail cache is
 * independent from the inventory module's listing cache. `item` is keyed by the scanned stock-item id.
 */
export const quickConsumptionKeys = {
  all: ['quick-consumption'] as const,
  item: (stockItemId: string) => [...quickConsumptionKeys.all, 'item', stockItemId] as const,
};

/**
 * Loads the scanned item's card (name, lot, on-hand balance, storage location) from
 * GET /api/inventory/stock-items/{id}. Disabled until a QR resolves to an id, so nothing is fetched
 * on the idle scanner screen. A 404 (unknown id / another tenant's item) surfaces as the query error,
 * which the page turns into a friendly "item não encontrado" state. `retry: false` keeps a wrong scan
 * from hammering the endpoint.
 *
 * The consumption mutation lives in the inventory module (`useRegisterConsumption`), which already
 * invalidates the stock-item listing on success — this flow reuses it rather than duplicating it.
 */
export function useStockItemDetail(stockItemId: string | null) {
  return useQuery({
    queryKey: quickConsumptionKeys.item(stockItemId ?? ''),
    queryFn: () => api.get<StockItemDetail>(Endpoints.inventory.stockItems.byId(stockItemId as string)),
    enabled: Boolean(stockItemId),
    retry: false,
    staleTime: 15_000,
  });
}
