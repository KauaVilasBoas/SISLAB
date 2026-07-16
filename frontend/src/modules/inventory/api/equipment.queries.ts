import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  ChangeEquipmentStatusRequest,
  DefineEquipmentCalibrationRequest,
  EquipmentDetail,
  EquipmentFilters,
  EquipmentListItem,
  RecordEquipmentMaintenanceRequest,
  RegisterEquipmentRequest,
  UpdateEquipmentRequest,
} from '@/modules/inventory/equipment.types';

/**
 * Equipment query keys (card [E7] #48), namespaced under 'inventory' > 'equipment' so a write
 * mutation can invalidate the whole equipment namespace (list of any page/filter + any open detail)
 * in one call without touching the stock-item or partner caches. `list` is parameterized by the
 * active filters and page so switching filters keeps its own cache entry.
 */
export const equipmentKeys = {
  all: ['inventory', 'equipment'] as const,
  list: (filters: EquipmentFilters, page: number) =>
    [...equipmentKeys.all, 'list', filters, page] as const,
  detail: (id: string) => [...equipmentKeys.all, 'detail', id] as const,
};

const PAGE_SIZE = 20;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/** Paginated, filterable equipment of the active company for the equipment table. */
export function useEquipmentList(filters: EquipmentFilters, page: number) {
  return useQuery({
    queryKey: equipmentKeys.list(filters, page),
    queryFn: () =>
      api.get<PagedResult<EquipmentListItem>>(Endpoints.inventory.equipment.root, {
        status: filters.status || undefined,
        storageLocationId: filters.storageLocationId || undefined,
        search: filters.search || undefined,
        page,
        pageSize: PAGE_SIZE,
      }),
    staleTime: 30_000,
  });
}

/** Detail of a single equipment. Disabled until an id is provided. */
export function useEquipmentDetail(id: string | undefined) {
  return useQuery({
    queryKey: equipmentKeys.detail(id ?? ''),
    queryFn: () =>
      api.get<EquipmentDetail>(Endpoints.inventory.equipment.byId(id as string)),
    enabled: Boolean(id),
    staleTime: 30_000,
  });
}

// ---------------------------------------------------------------------------
// Mutations — each invalidates the whole equipment namespace (list + details).
// ---------------------------------------------------------------------------

/** Registers a new equipment; refreshes the equipment list. */
export function useRegisterEquipment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RegisterEquipmentRequest) =>
      api.post<string>(Endpoints.inventory.equipment.root, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: equipmentKeys.all }),
  });
}

/** Updates an equipment's identification data; refreshes list and detail. */
export function useUpdateEquipment(equipmentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: UpdateEquipmentRequest) =>
      api.put<void>(Endpoints.inventory.equipment.byId(equipmentId), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: equipmentKeys.all }),
  });
}

/** Moves an equipment to a new operational status; refreshes list and detail. */
export function useChangeEquipmentStatus(equipmentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ChangeEquipmentStatusRequest) =>
      api.post<void>(Endpoints.inventory.equipment.status(equipmentId), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: equipmentKeys.all }),
  });
}

/** Defines/clears an equipment's calibration schedule; refreshes list and detail. */
export function useDefineEquipmentCalibration(equipmentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: DefineEquipmentCalibrationRequest) =>
      api.put<void>(Endpoints.inventory.equipment.calibration(equipmentId), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: equipmentKeys.all }),
  });
}

/** Logs a maintenance event against an equipment; refreshes list and detail. */
export function useRecordEquipmentMaintenance(equipmentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordEquipmentMaintenanceRequest) =>
      api.post<void>(Endpoints.inventory.equipment.maintenances(equipmentId), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: equipmentKeys.all }),
  });
}
