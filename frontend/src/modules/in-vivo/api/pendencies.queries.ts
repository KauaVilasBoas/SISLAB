import { useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PendenciesResult } from '@/modules/in-vivo/types';

/** Pendencies panel query key. */
export const pendencyKeys = {
  all: ['in-vivo', 'pendencies'] as const,
};

/** The active company's open pendencies with per-kind summary counts (card [E11] #90). */
export function usePendencies() {
  return useQuery({
    queryKey: pendencyKeys.all,
    queryFn: () => api.get<PendenciesResult>(Endpoints.experiments.pendencies),
    // Refresh on focus so the panel reflects work done elsewhere without a manual reload.
    refetchOnWindowFocus: true,
  });
}
