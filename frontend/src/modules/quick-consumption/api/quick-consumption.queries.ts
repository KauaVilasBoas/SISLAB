import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type { LocationSummaryItem, StockItemListItem } from '@/modules/inventory/types';
import type { StockItemDetail } from '@/modules/quick-consumption/types';

/**
 * Quick-consumption query keys, namespaced under 'quick-consumption' so a scan's item-detail cache is
 * independent from the inventory module's listing cache. `item` is keyed by the scanned stock-item id;
 * `locationItems` by the scanned storage-location id (the cabinet's item picker, card [E7] #92).
 */
export const quickConsumptionKeys = {
  all: ['quick-consumption'] as const,
  item: (stockItemId: string) => [...quickConsumptionKeys.all, 'item', stockItemId] as const,
  locationItems: (storageLocationId: string) =>
    [...quickConsumptionKeys.all, 'location-items', storageLocationId] as const,
};

/** Upper bound on items listed for a scanned location — a single cabinet never holds more in practice. */
const LOCATION_ITEMS_LIMIT = 200;

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

/** The resolved cabinet plus the items it holds, after scanning a `sislab:location:<guid>` QR. */
export interface ScannedLocation {
  /** The location's display name, or null when the id resolves to no active company location. */
  name: string | null;
  /** True when the scanned id matches one of the active company's locations. */
  found: boolean;
  items: StockItemListItem[];
}

/**
 * Resolves a scanned storage-location QR (card [E7] #92): its display name (from the location summary the
 * inventory module already exposes — no new endpoint) and the items it holds (the tenant-scoped listing
 * filtered by `storageLocationId`, which #63 confirmed exists). Disabled until a location QR resolves.
 *
 * Both round-trips are tenant-scoped by the backend, so an id from another company resolves to "not
 * found" (null name, empty items) exactly like an unknown one — the page shows the same friendly state.
 */
export function useScannedLocation(storageLocationId: string | null) {
  const locations = useQuery({
    queryKey: [...quickConsumptionKeys.all, 'locations'] as const,
    queryFn: () =>
      api.get<LocationSummaryItem[]>(Endpoints.inventory.storageLocations.summary),
    enabled: Boolean(storageLocationId),
    staleTime: 5 * 60_000,
  });

  const items = useQuery({
    queryKey: quickConsumptionKeys.locationItems(storageLocationId ?? ''),
    queryFn: () =>
      api.get<PagedResult<StockItemListItem>>(Endpoints.inventory.stockItems.list, {
        storageLocationId: storageLocationId as string,
        page: 1,
        pageSize: LOCATION_ITEMS_LIMIT,
      }),
    enabled: Boolean(storageLocationId),
    retry: false,
    staleTime: 15_000,
  });

  const location = useMemo<LocationSummaryItem | undefined>(
    () => locations.data?.find((l) => l.id === storageLocationId),
    [locations.data, storageLocationId],
  );

  const data = useMemo<ScannedLocation | undefined>(() => {
    if (!locations.data || !items.data) return undefined;
    return {
      name: location?.name ?? null,
      found: Boolean(location),
      items: items.data.items,
    };
  }, [locations.data, items.data, location]);

  return {
    data,
    isLoading: locations.isLoading || items.isLoading,
    isError: locations.isError || items.isError,
    error: locations.error ?? items.error,
  };
}
