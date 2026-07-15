import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  BelowMinimumSummary,
  ConsumptionSeries,
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
};

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

/** Consumption time series over [from, to] (ISO dates). Feeds the bar chart. */
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
