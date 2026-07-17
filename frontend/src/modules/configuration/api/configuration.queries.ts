import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  CreateItemCategoryRequest,
  CreateReferenceRangeRequest,
  CreateRoomRequest,
  CreateUnitRequest,
  ItemCategoryListItem,
  ReferenceRangeListItem,
  RoomListItem,
  SetExpiryWarningWindowRequest,
  UnitListItem,
} from '@/modules/configuration/types';

/**
 * Configuration module query keys, namespaced under 'configuration' so each catalogue's mutation
 * invalidates only its own list without touching the other tabs or unrelated modules.
 */
export const configurationKeys = {
  all: ['configuration'] as const,
  units: () => [...configurationKeys.all, 'units'] as const,
  rooms: () => [...configurationKeys.all, 'rooms'] as const,
  itemCategories: () => [...configurationKeys.all, 'item-categories'] as const,
  referenceRanges: () => [...configurationKeys.all, 'reference-ranges'] as const,
  expiryPolicy: () => [...configurationKeys.all, 'expiry-policy'] as const,
};

// The catalogues change rarely; keep them fresh for a couple of minutes to avoid refetch churn.
const CATALOGUE_STALE_TIME = 2 * 60_000;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/** The active company's units of measure, ordered by symbol. */
export function useUnits() {
  return useQuery({
    queryKey: configurationKeys.units(),
    queryFn: () => api.get<UnitListItem[]>(Endpoints.configuration.units),
    staleTime: CATALOGUE_STALE_TIME,
  });
}

/** The active company's rooms, ordered by name. */
export function useRooms() {
  return useQuery({
    queryKey: configurationKeys.rooms(),
    queryFn: () => api.get<RoomListItem[]>(Endpoints.configuration.rooms),
    staleTime: CATALOGUE_STALE_TIME,
  });
}

/** The active company's item categories, ordered by name. */
export function useItemCategories() {
  return useQuery({
    queryKey: configurationKeys.itemCategories(),
    queryFn: () => api.get<ItemCategoryListItem[]>(Endpoints.configuration.itemCategories),
    staleTime: CATALOGUE_STALE_TIME,
  });
}

/** The active company's reference ranges, ordered by analyte then species. */
export function useReferenceRanges() {
  return useQuery({
    queryKey: configurationKeys.referenceRanges(),
    queryFn: () => api.get<ReferenceRangeListItem[]>(Endpoints.configuration.referenceRanges),
    staleTime: CATALOGUE_STALE_TIME,
  });
}

/** The active company's expiry warning window, in days before expiry (the default when unset). */
export function useExpiryPolicy() {
  return useQuery({
    queryKey: configurationKeys.expiryPolicy(),
    queryFn: () => api.get<number>(Endpoints.configuration.expiryPolicy),
    staleTime: CATALOGUE_STALE_TIME,
  });
}

// ---------------------------------------------------------------------------
// Mutations
// ---------------------------------------------------------------------------

/** Creates a unit; refreshes the units list. */
export function useCreateUnit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateUnitRequest) =>
      api.post<string>(Endpoints.configuration.units, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: configurationKeys.units() }),
  });
}

/** Creates a room; refreshes the rooms list. */
export function useCreateRoom() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateRoomRequest) =>
      api.post<string>(Endpoints.configuration.rooms, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: configurationKeys.rooms() }),
  });
}

/** Creates an item category; refreshes the item-categories list. */
export function useCreateItemCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateItemCategoryRequest) =>
      api.post<string>(Endpoints.configuration.itemCategories, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: configurationKeys.itemCategories() }),
  });
}

/** Creates a reference range; refreshes the reference-ranges list. */
export function useCreateReferenceRange() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateReferenceRangeRequest) =>
      api.post<string>(Endpoints.configuration.referenceRanges, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: configurationKeys.referenceRanges() }),
  });
}

/** Sets the expiry warning window; refreshes the cached policy value. */
export function useSetExpiryPolicy() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: SetExpiryWarningWindowRequest) =>
      api.put<void>(Endpoints.configuration.expiryPolicy, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: configurationKeys.expiryPolicy() }),
  });
}
