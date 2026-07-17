import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  PartnerFilters,
  PartnerListItem,
  RegisterPartnerRequest,
  UpdatePartnerRequest,
} from '@/modules/inventory/partner.types';

/**
 * Partner query keys (card [E7] #48), namespaced under 'inventory' > 'partners' so a write mutation
 * can invalidate the whole partner namespace (list of any page/filter + any open detail) in one call
 * without touching the stock-item or equipment caches. `list` is parameterized by the active filters
 * and page so switching filters keeps its own cache entry.
 */
export const partnerKeys = {
  all: ['inventory', 'partners'] as const,
  list: (filters: PartnerFilters, page: number) =>
    [...partnerKeys.all, 'list', filters, page] as const,
};

const PAGE_SIZE = 20;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/** Paginated, filterable partners of the active company for the partners grid. */
export function usePartnerList(filters: PartnerFilters, page: number) {
  return useQuery({
    queryKey: partnerKeys.list(filters, page),
    queryFn: () =>
      api.get<PagedResult<PartnerListItem>>(Endpoints.inventory.partners.root, {
        type: filters.type || undefined,
        search: filters.search || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });
}

// ---------------------------------------------------------------------------
// Mutations — each invalidates the whole partner namespace (list + details).
// ---------------------------------------------------------------------------

/** Registers a new partner; refreshes the partner list. */
export function useRegisterPartner() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterPartnerRequest) =>
      api.post<string>(Endpoints.inventory.partners.root, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partnerKeys.all }),
  });
}

/** Updates a partner's descriptive data; refreshes list and detail. */
export function useUpdatePartner(partnerId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdatePartnerRequest) =>
      api.put<void>(Endpoints.inventory.partners.byId(partnerId), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partnerKeys.all }),
  });
}

/** Takes a partner out of service; refreshes list and detail. */
export function useDeactivatePartner(partnerId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<void>(Endpoints.inventory.partners.deactivate(partnerId)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partnerKeys.all }),
  });
}

/** Puts a deactivated partner back in service; refreshes list and detail. */
export function useReactivatePartner(partnerId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<void>(Endpoints.inventory.partners.reactivate(partnerId)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partnerKeys.all }),
  });
}
