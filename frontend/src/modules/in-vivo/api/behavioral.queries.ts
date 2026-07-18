import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, httpClient } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import { experimentKeys } from '@/modules/experiments/api/experiments.queries';
import type {
  CreateBehavioralExperimentRequest,
  RecordTimepointRequest,
} from '@/modules/in-vivo/types';

/**
 * Behavioural in vivo mutations (card [E11] #88): a behavioural experiment shares the Experiments module's
 * list/detail read model, so its writes invalidate the shared `experimentKeys` — the in vivo launch shows up in
 * the same experiments list and detail the in vitro slice uses.
 */

/** Creates a behavioural experiment (von Frey / tail-flick / rota-rod / hemogram); returns the new id. */
export function useCreateBehavioralExperiment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateBehavioralExperimentRequest) =>
      api.post<string>(Endpoints.experiments.createBehavioral, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: experimentKeys.all }),
  });
}

/** Records one timepoint's readings (one per animal). Invalidates the experiment detail + list. */
export function useRecordTimepoint(experimentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordTimepointRequest) =>
      api.post<void>(Endpoints.experiments.recordTimepoint(experimentId), body),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: experimentKeys.detail(experimentId),
      });
      void queryClient.invalidateQueries({ queryKey: experimentKeys.all });
    },
  });
}

/** Runs the versioned behavioural calculation and freezes its snapshot. Invalidates the experiment detail. */
export function useCalculateBehavioralExperiment(experimentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      api.post<void>(Endpoints.experiments.calculateBehavioral(experimentId), {}),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: experimentKeys.detail(experimentId),
      });
      void queryClient.invalidateQueries({ queryKey: experimentKeys.all });
    },
  });
}

/**
 * Downloads the in vivo Prism export (group × timepoint, card #31). The endpoint returns a raw text/csv body, so
 * it bypasses the `api` helper and reads the blob directly, then triggers a browser download.
 */
export function useExportBehavioralExperiment(experimentId: string) {
  return useMutation({
    mutationFn: async () => {
      const response = await httpClient.get<Blob>(
        Endpoints.experiments.exportBehavioral(experimentId),
        { responseType: 'blob' },
      );

      const disposition = String(response.headers['content-disposition'] ?? '');
      const match = /filename="?([^"]+)"?/.exec(disposition);
      const fileName = match?.[1] ?? `experimento-invivo-${experimentId}.csv`;

      const url = URL.createObjectURL(response.data);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    },
  });
}
