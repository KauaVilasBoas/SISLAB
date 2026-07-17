import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type { EquipmentListItem } from '@/modules/inventory/equipment.types';
import type {
  BelowMinimumItem,
  BelowMinimumSummary,
  ConsumptionSeries,
  ExpiringItem,
  ExpirySummary,
} from '@/modules/dashboard/types';

/**
 * Module-scoped query keys. Namespaced under 'dashboard' so cache invalidation
 * can target this module without touching others.
 */
export const dashboardKeys = {
  all: ['dashboard'] as const,
  expirySummary: () => [...dashboardKeys.all, 'expiry-summary'] as const,
  belowMinimum: () => [...dashboardKeys.all, 'below-minimum'] as const,
  consumptionSeries: (from: string, to: string) =>
    [...dashboardKeys.all, 'consumption-series', from, to] as const,
  expiringItems: () => [...dashboardKeys.all, 'expiring-items'] as const,
  belowMinimumItems: () => [...dashboardKeys.all, 'below-minimum-items'] as const,
  overdueCalibration: () => [...dashboardKeys.all, 'overdue-calibration'] as const,
};

/** How many rows the active-alerts widget pulls per category (it shows a short, prioritized list). */
const ALERTS_PAGE_SIZE = 5;

export function useExpirySummary() {
  return useQuery({
    queryKey: dashboardKeys.expirySummary(),
    queryFn: () => api.get<ExpirySummary>(Endpoints.inventory.stockItems.expirySummary),
  });
}

export function useBelowMinimumSummary() {
  return useQuery({
    queryKey: dashboardKeys.belowMinimum(),
    queryFn: () =>
      api.get<BelowMinimumSummary>(Endpoints.inventory.stockItems.belowMinimumSummary),
  });
}

/** Consumption time series over [from, to] (ISO dates). Feeds the activity chart. */
export function useConsumptionSeries(from: string, to: string) {
  return useQuery({
    queryKey: dashboardKeys.consumptionSeries(from, to),
    queryFn: () =>
      api.get<ConsumptionSeries>(Endpoints.inventory.reports.consumptionSeries, {
        from,
        to,
      }),
  });
}

/**
 * Top expiring/expired items for the active-alerts list. Includes already-expired items (ordered
 * validity-ascending, so the most critical lead), and carries `isControlled` so the KPI can count
 * expired controlled drugs.
 */
export function useExpiringItems() {
  return useQuery({
    queryKey: dashboardKeys.expiringItems(),
    queryFn: () =>
      api.get<PagedResult<ExpiringItem>>(Endpoints.inventory.stockItems.expiring, {
        includeExpired: true,
        page: 1,
        pageSize: ALERTS_PAGE_SIZE,
      }),
  });
}

/** Top below-minimum items (largest deficit first) for the active-alerts list. */
export function useBelowMinimumItems() {
  return useQuery({
    queryKey: dashboardKeys.belowMinimumItems(),
    queryFn: () =>
      api.get<PagedResult<BelowMinimumItem>>(Endpoints.inventory.stockItems.belowMinimum, {
        page: 1,
        pageSize: ALERTS_PAGE_SIZE,
      }),
  });
}

/** Equipment whose calibration is overdue (status=Overdue) for the active-alerts list. */
export function useOverdueCalibration() {
  return useQuery({
    queryKey: dashboardKeys.overdueCalibration(),
    queryFn: () =>
      api.get<PagedResult<EquipmentListItem>>(Endpoints.inventory.equipment.root, {
        status: 'Overdue',
        page: 1,
        pageSize: ALERTS_PAGE_SIZE,
      }),
  });
}
