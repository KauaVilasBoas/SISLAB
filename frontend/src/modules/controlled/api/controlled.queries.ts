import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, httpClient } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import { inventoryKeys } from '@/modules/inventory/api/inventory.queries';
import type { StockItemListItem } from '@/modules/inventory/types';
import type {
  AuditTrailEntry,
  ControlledFilters,
  RegisterStockCountRequest,
} from '@/modules/controlled/types';

/**
 * Controlados module query keys (card [E7] #62). Namespaced under 'controlled' so the compliance
 * screen caches its own listing/trail per filter/page, and the conference mutation can invalidate the
 * controlled list without touching the general inventory cache. The mutation ALSO invalidates the
 * shared inventory namespace, because a controlled item is a stock item — any Estoque view of it must
 * refetch after a count writes to the trail.
 */
export const controlledKeys = {
  all: ['controlled'] as const,
  items: (filters: ControlledFilters, page: number) =>
    [...controlledKeys.all, 'items', filters, page] as const,
  trail: (entityId: string | undefined, page: number) =>
    [...controlledKeys.all, 'trail', entityId ?? 'all', page] as const,
};

const PAGE_SIZE = 20;

/** Entity type stamped by the Inventory audit writer — narrows the trail to stock-item actions. */
const STOCK_ITEM_ENTITY_TYPE = 'StockItem';

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/**
 * Paginated list of the active company's controlled substances (is_controlled = true), reusing the
 * inventory stock-items endpoint with the controlled-only server filter (card [E7] #62). Feeds the
 * compliance table and the "N of M expired" banner.
 */
export function useControlledItems(filters: ControlledFilters, page: number) {
  return useQuery({
    queryKey: controlledKeys.items(filters, page),
    queryFn: () =>
      api.get<PagedResult<StockItemListItem>>(Endpoints.inventory.stockItems.list, {
        isControlled: true,
        search: filters.search || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });
}

/**
 * Paginated append-only audit trail of controlled operations (consumption, disposal, conference),
 * newest first, narrowed to the StockItem entity type. Reads the Audit module endpoint (card #57).
 * When <paramref name="entityId"/> is supplied the trail is scoped to a single item — the per-item "log"
 * view triggered from the table.
 */
export function useControlledAuditTrail(entityId: string | undefined, page: number) {
  return useQuery({
    queryKey: controlledKeys.trail(entityId, page),
    queryFn: () =>
      api.get<PagedResult<AuditTrailEntry>>(Endpoints.audit.entries, {
        entityType: STOCK_ITEM_ENTITY_TYPE,
        entityId: entityId || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });
}

// ---------------------------------------------------------------------------
// Mutation — register a conference (physical count)
// ---------------------------------------------------------------------------

/**
 * Registers a physical count (conference) of a controlled item. The backend returns the divergence
 * (counted minus system balance) and does not change the balance — it only appends the compliance
 * record to the trail. On success both the controlled list and the trail are invalidated so the
 * screen reflects the new audit entry; the shared inventory cache is invalidated too.
 */
export function useRegisterConference(stockItemId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterStockCountRequest) =>
      api.post<number>(Endpoints.inventory.stockItems.counts(stockItemId), body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: controlledKeys.all });
      queryClient.invalidateQueries({ queryKey: inventoryKeys.stockItems() });
    },
  });
}

// ---------------------------------------------------------------------------
// Export — download the filtered audit trail as CSV
// ---------------------------------------------------------------------------

/**
 * Downloads the controlled audit trail (StockItem entity type, no pagination) as a CSV attachment
 * (card #57). The export endpoint streams a file rather than the JSON envelope, so it bypasses the
 * `api` unwrap helper and reads the raw blob, then triggers a browser download. Kept as a plain async
 * function (not a hook) because it is fire-and-forget from a button handler.
 */
export async function downloadControlledAuditTrailCsv(entityId?: string): Promise<void> {
  const response = await httpClient.get<Blob>(Endpoints.audit.entriesExport, {
    params: { entityType: STOCK_ITEM_ENTITY_TYPE, entityId: entityId || undefined },
    responseType: 'blob',
  });

  const url = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = url;
  link.download = `controlados-trilha-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
