import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  ApplyDilutionSchemeRequest,
  DilutionScheme,
  DilutionSchemeParams,
} from '@/modules/experiments/types';
import { experimentKeys } from '@/modules/experiments/api/experiments.queries';

/** Serial-dilution query keys (SISLAB-05), namespaced under the experiments module. */
export const dilutionKeys = {
  all: ['experiments', 'dilution'] as const,
  scheme: (params: DilutionSchemeParams) => [...dilutionKeys.all, 'scheme', params] as const,
};

/**
 * Serializes the compute params, dropping every `undefined`/empty value so an omitted optional (a skipped stock or
 * DMSO control) adds nothing to the query string — the backend then computes only the parts the operator asked for.
 */
function buildSchemeQuery(params: DilutionSchemeParams): Record<string, unknown> {
  const query: Record<string, unknown> = {};
  (Object.entries(params) as [keyof DilutionSchemeParams, unknown][]).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      query[key] = value;
    }
  });
  return query;
}

/**
 * Computes the serial-dilution scheme (SISLAB-05) — stateless, so a plain query keyed by the inputs. Disabled until
 * the required series inputs are valid (positive concentration/volume, factor > 1, at least one point), so the UI
 * never fires a request the backend would reject; the caller flips {@link enabled} when the form is complete.
 */
export function useDilutionScheme(params: DilutionSchemeParams, enabled: boolean) {
  return useQuery({
    queryKey: dilutionKeys.scheme(params),
    queryFn: () =>
      api.get<DilutionScheme>(Endpoints.experiments.dilutionScheme, buildSchemeQuery(params)),
    enabled,
  });
}

/**
 * Populates a plate column's concentrations from a serial-dilution scheme (SISLAB-05). Invalidates the target
 * experiment's detail + plate result so the grid reflects the newly written ConcentrationUm wells.
 */
export function useApplyDilutionScheme(experimentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: ApplyDilutionSchemeRequest) =>
      api.post<void>(Endpoints.experiments.applyDilutionScheme(experimentId), body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: experimentKeys.detail(experimentId) });
      void queryClient.invalidateQueries({ queryKey: experimentKeys.plateResult(experimentId) });
    },
  });
}
