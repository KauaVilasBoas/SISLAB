import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api, httpClient } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  CreateExperimentRequest,
  DesignPlateWellRequest,
  ExperimentDetail,
  ExperimentListItem,
  PlateReadingResult,
} from '@/modules/experiments/types';

/**
 * Experiments module query keys, namespaced under 'experiments' so a write invalidates only this
 * module's lists/details without touching unrelated modules.
 */
export const experimentKeys = {
  all: ['experiments'] as const,
  list: (params: ListExperimentsParams) => [...experimentKeys.all, 'list', params] as const,
  detail: (id: string) => [...experimentKeys.all, 'detail', id] as const,
  plateResult: (id: string) => [...experimentKeys.all, 'plate-result', id] as const,
};

export interface ListExperimentsParams {
  page: number;
  pageSize: number;
  status?: string;
}

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/** Paginated experiments of the active company, optionally filtered by status. */
export function useExperiments(params: ListExperimentsParams) {
  return useQuery({
    queryKey: experimentKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<ExperimentListItem>>(Endpoints.experiments.root, {
        page: params.page,
        pageSize: params.pageSize,
        status: params.status || undefined,
      }),
  });
}

/** A single experiment's detail (header, steps, plate wells, calculation snapshot). */
export function useExperiment(id: string) {
  return useQuery({
    queryKey: experimentKeys.detail(id),
    queryFn: () => api.get<ExperimentDetail>(Endpoints.experiments.byId(id)),
    enabled: Boolean(id),
  });
}

/** The experiment's 8×12 plate result grid (readings + % viability). */
export function usePlateResult(id: string) {
  return useQuery({
    queryKey: experimentKeys.plateResult(id),
    queryFn: () => api.get<PlateReadingResult>(Endpoints.experiments.plateResult(id)),
    enabled: Boolean(id),
  });
}

// ---------------------------------------------------------------------------
// Mutations
// ---------------------------------------------------------------------------

/** Creates a plate experiment (viability or nitric oxide); returns the new id. Invalidates the list. */
export function useCreateExperiment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateExperimentRequest) =>
      api.post<string>(Endpoints.experiments.root, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: experimentKeys.all }),
  });
}

/** Lays out the plate wells for an experiment. Invalidates its detail + plate result. */
export function useDesignPlate(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (wells: DesignPlateWellRequest[]) =>
      api.post<void>(Endpoints.experiments.designPlate(id), { wells }),
    onSuccess: () => invalidateExperiment(queryClient, id),
  });
}

/** Imports the plate reader CSV for an experiment. Invalidates its detail + plate result. */
export function useImportReading(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (csvContent: string) =>
      api.post<void>(Endpoints.experiments.importReading(id), { csvContent }),
    onSuccess: () => invalidateExperiment(queryClient, id),
  });
}

/** Runs the versioned calculation (viability or nitric oxide). Invalidates its detail + plate result. */
export function useCalculateExperiment(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.post<void>(Endpoints.experiments.calculate(id), {}),
    onSuccess: () => invalidateExperiment(queryClient, id),
  });
}

/**
 * Downloads the experiment's GraphPad Prism CSV export (card #79). The endpoint returns a raw text/csv body
 * (not the ApiResult envelope), so it bypasses the `api` helper and reads the blob directly, then triggers a
 * browser download from the Content-Disposition filename.
 */
export function useExportExperiment(id: string) {
  return useMutation({
    mutationFn: async () => {
      const response = await httpClient.get<Blob>(Endpoints.experiments.export(id), {
        responseType: 'blob',
      });

      const disposition = String(response.headers['content-disposition'] ?? '');
      const match = /filename="?([^"]+)"?/.exec(disposition);
      const fileName = match?.[1] ?? `experimento-${id}.csv`;

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

function invalidateExperiment(
  queryClient: ReturnType<typeof useQueryClient>,
  id: string,
): void {
  void queryClient.invalidateQueries({ queryKey: experimentKeys.detail(id) });
  void queryClient.invalidateQueries({ queryKey: experimentKeys.plateResult(id) });
  void queryClient.invalidateQueries({ queryKey: experimentKeys.all });
}
