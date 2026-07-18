import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  AnalyseSampleRequest,
  CollectSampleRequest,
  RecordAnalysisResultRequest,
  SampleDetail,
  SampleListItem,
} from '@/modules/in-vivo/types';

/** Biobank query keys, namespaced so a write invalidates only this module's lists/details. */
export const sampleKeys = {
  all: ['in-vivo', 'samples'] as const,
  list: (params: ListSamplesParams) => [...sampleKeys.all, 'list', params] as const,
  detail: (id: string) => [...sampleKeys.all, 'detail', id] as const,
};

export interface ListSamplesParams {
  page: number;
  pageSize: number;
  projectId?: string;
  type?: string;
}

/** Paginated biobank samples of the active company, with the derived remaining balance. */
export function useSamples(params: ListSamplesParams) {
  return useQuery({
    queryKey: sampleKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<SampleListItem>>(Endpoints.samples.root, {
        page: params.page,
        pageSize: params.pageSize,
        projectId: params.projectId || undefined,
        type: params.type || undefined,
      }),
  });
}

/** A single sample's detail (derived balance + analyses). */
export function useSample(id: string) {
  return useQuery({
    queryKey: sampleKeys.detail(id),
    queryFn: () => api.get<SampleDetail>(Endpoints.samples.byId(id)),
    enabled: Boolean(id),
  });
}

/** Collects a sample; returns the new id. Invalidates the list. */
export function useCollectSample() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CollectSampleRequest) =>
      api.post<string>(Endpoints.samples.root, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sampleKeys.all }),
  });
}

/** Runs an analysis against a sample (consumes an aliquot). Invalidates its detail + the list. */
export function useAnalyseSample(sampleId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AnalyseSampleRequest) =>
      api.post<string>(Endpoints.samples.analyses(sampleId), body),
    onSuccess: () => invalidateSample(queryClient, sampleId),
  });
}

/** Records the result of a pending analysis. Invalidates the sample detail. */
export function useRecordAnalysisResult(sampleId: string, analysisId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: RecordAnalysisResultRequest) =>
      api.post<void>(Endpoints.samples.analysisResult(sampleId, analysisId), body),
    onSuccess: () => invalidateSample(queryClient, sampleId),
  });
}

function invalidateSample(
  queryClient: ReturnType<typeof useQueryClient>,
  sampleId: string,
): void {
  void queryClient.invalidateQueries({ queryKey: sampleKeys.detail(sampleId) });
  void queryClient.invalidateQueries({ queryKey: sampleKeys.all });
}
