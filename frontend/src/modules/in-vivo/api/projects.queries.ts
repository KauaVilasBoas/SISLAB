import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  AddAnimalRequest,
  AddCageRequest,
  AddGroupRequest,
  AnimalSelectionListItem,
  AssignAnimalToGroupRequest,
  BindBatchModelRequest,
  CageBaselineItem,
  CreateProjectRequest,
  GroupBaselineItem,
  PhysiologicalReadingListItem,
  PrepareGroupSolutionRequest,
  ProjectDetail,
  ProjectListItem,
  RecordReadingRequest,
  SolutionPreparationListItem,
} from '@/modules/in-vivo/types';

/** In vivo project query keys, namespaced so a write invalidates only this module's lists/details. */
export const projectKeys = {
  all: ['in-vivo', 'projects'] as const,
  list: (params: ListProjectsParams) => [...projectKeys.all, 'list', params] as const,
  detail: (id: string) => [...projectKeys.all, 'detail', id] as const,
  preparations: (id: string) => [...projectKeys.all, 'preparations', id] as const,
  baselineByCage: (projectId: string, batchId: string, parameterCode: string) =>
    [
      ...projectKeys.all,
      'baseline',
      'by-cage',
      projectId,
      batchId,
      parameterCode,
    ] as const,
  baselineByGroup: (projectId: string, batchId: string, parameterCode: string) =>
    [
      ...projectKeys.all,
      'baseline',
      'by-group',
      projectId,
      batchId,
      parameterCode,
    ] as const,
  readings: (projectId: string, parameterCode: string | null, animalId: string | null) =>
    [
      ...projectKeys.all,
      'readings',
      projectId,
      parameterCode ?? null,
      animalId ?? null,
    ] as const,
  selection: (projectId: string, batchId: string, status: string | null) =>
    [...projectKeys.all, 'selection', projectId, batchId, status ?? null] as const,
};

export interface ListProjectsParams {
  page: number;
  pageSize: number;
  status?: string;
}

/** Paginated in vivo projects of the active company, optionally filtered by status. */
export function useProjects(params: ListProjectsParams) {
  return useQuery({
    queryKey: projectKeys.list(params),
    queryFn: () =>
      api.get<PagedResult<ProjectListItem>>(Endpoints.projects.root, {
        page: params.page,
        pageSize: params.pageSize,
        status: params.status || undefined,
      }),
  });
}

/** A single project's full delineation (batches, groups, animals). */
export function useProject(id: string) {
  return useQuery({
    queryKey: projectKeys.detail(id),
    queryFn: () => api.get<ProjectDetail>(Endpoints.projects.byId(id)),
    enabled: Boolean(id),
  });
}

/** Creates a project; returns the new id. Invalidates the list. */
export function useCreateProject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateProjectRequest) =>
      api.post<string>(Endpoints.projects.root, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: projectKeys.all }),
  });
}

/** Adds a batch (leva) to a project. Invalidates its detail + the list (batch count). */
export function useAddBatch(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) =>
      api.post<string>(Endpoints.projects.batches(projectId), { name }),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/** Adds a dose group to a batch. Invalidates the project detail. */
export function useAddGroup(projectId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AddGroupRequest) =>
      api.post<string>(Endpoints.projects.groups(projectId, batchId), body),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/** Adds a cage (caixa) to a batch (SISLAB-03). Invalidates the project detail. */
export function useAddCage(projectId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AddCageRequest) =>
      api.post<string>(Endpoints.projects.cages(projectId, batchId), body),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/**
 * Houses an animal in a cage (SISLAB-03) — the new pre-randomization entry flow. The group is optional
 * (assign later). Invalidates the project detail + the list (animal count).
 */
export function useAddAnimal(projectId: string, batchId: string, cageId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AddAnimalRequest) =>
      api.post<string>(Endpoints.projects.cageAnimals(projectId, batchId, cageId), body),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/**
 * Assigns (or moves) an animal to a treatment group after basal/induction (SISLAB-03) — including
 * redistributing a discrepant cage. The backend rejects assignment once the leva starts (frozen design);
 * the UI hides the action then. Invalidates the project detail so the new assignment is reflected.
 */
export function useAssignAnimalToGroup(projectId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      animalId,
      body,
    }: {
      animalId: string;
      body: AssignAnimalToGroupRequest;
    }) =>
      api.put<void>(Endpoints.projects.animalGroup(projectId, batchId, animalId), body),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/** Starts a batch (freezes its design, activates the project). Invalidates the project detail. */
export function useStartBatch(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (batchId: string) =>
      api.post<void>(Endpoints.projects.startBatch(projectId, batchId), {}),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/**
 * Binds a batch (leva) to an experimental model / induction protocol (SISLAB-04). The backend rejects binding
 * a started (frozen) batch; the UI already hides the action then. Invalidates the project detail so the bound
 * model is reflected.
 */
export function useBindBatchModel(projectId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: BindBatchModelRequest) =>
      api.put<void>(Endpoints.projects.batchModel(projectId, batchId), body),
    onSuccess: () => invalidateProject(queryClient, projectId),
  });
}

/** A project's confirmed in vivo solution preparations (SISLAB-01) — the frozen dose × weight snapshots. */
export function usePreparations(projectId: string) {
  return useQuery({
    queryKey: projectKeys.preparations(projectId),
    queryFn: () =>
      api.get<SolutionPreparationListItem[]>(
        Endpoints.projects.listPreparations(projectId),
      ),
    enabled: Boolean(projectId),
  });
}

/**
 * Confirms an in vivo solution preparation for a dose group (SISLAB-01) — a frozen, traceable snapshot.
 * Returns the new preparation id; invalidates the preparations list so the panel refreshes.
 */
export function usePrepareGroupSolution(
  projectId: string,
  batchId: string,
  groupId: string,
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: PrepareGroupSolutionRequest) =>
      api.post<string>(
        Endpoints.projects.preparations(projectId, batchId, groupId),
        body,
      ),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: projectKeys.preparations(projectId) }),
  });
}

/**
 * Baseline of one physiological parameter summarized by cage (SISLAB-03) — the pre-randomization view
 * (mean/min/max per cage). Enabled only once a parameter code is chosen; an optional timepoint narrows it.
 */
export function useBaselineByCage(
  projectId: string,
  batchId: string,
  parameterCode: string,
  timepointLabel?: string,
) {
  return useQuery({
    queryKey: [
      ...projectKeys.baselineByCage(projectId, batchId, parameterCode),
      timepointLabel ?? null,
    ],
    queryFn: () =>
      api.get<CageBaselineItem[]>(Endpoints.projects.baselineByCage(projectId, batchId), {
        parameterCode,
        timepointLabel: timepointLabel || undefined,
      }),
    enabled: Boolean(projectId) && Boolean(batchId) && parameterCode.trim().length > 0,
  });
}

/**
 * Baseline of one physiological parameter summarized by treatment group (SISLAB-03) — the
 * post-randomization balance-check view. Enabled only once a parameter code is chosen.
 */
export function useBaselineByGroup(
  projectId: string,
  batchId: string,
  parameterCode: string,
  timepointLabel?: string,
) {
  return useQuery({
    queryKey: [
      ...projectKeys.baselineByGroup(projectId, batchId, parameterCode),
      timepointLabel ?? null,
    ],
    queryFn: () =>
      api.get<GroupBaselineItem[]>(
        Endpoints.projects.baselineByGroup(projectId, batchId),
        {
          parameterCode,
          timepointLabel: timepointLabel || undefined,
        },
      ),
    enabled: Boolean(projectId) && Boolean(batchId) && parameterCode.trim().length > 0,
  });
}

/**
 * Records a physiological reading (glicemia/peso, … — SISLAB-02) on a project animal at a timepoint. Invalidates
 * the readings list, the batch selection (a new reading can change a decision on the next apply) and the baseline
 * summaries (they aggregate the same readings). Returns the new reading id.
 */
export function useRecordReading(projectId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ animalId, body }: { animalId: string; body: RecordReadingRequest }) =>
      api.post<string>(Endpoints.projects.animalReadings(projectId, animalId), body),
    onSuccess: () => {
      void queryClient.invalidateQueries({
        queryKey: [...projectKeys.all, 'readings', projectId],
      });
      void queryClient.invalidateQueries({
        queryKey: [...projectKeys.all, 'selection', projectId],
      });
      void queryClient.invalidateQueries({ queryKey: [...projectKeys.all, 'baseline'] });
    },
  });
}

/**
 * A project's physiological readings (SISLAB-02), optionally narrowed by parameter code and/or a single animal.
 * A blank parameter code lists every parameter; a null animal lists every animal.
 */
export function useReadings(
  projectId: string,
  parameterCode?: string,
  animalId?: string | null,
) {
  const code = parameterCode?.trim() || null;
  const animal = animalId ?? null;
  return useQuery({
    queryKey: projectKeys.readings(projectId, code, animal),
    queryFn: () =>
      api.get<PhysiologicalReadingListItem[]>(Endpoints.projects.readings(projectId), {
        parameterCode: code || undefined,
        animalId: animal || undefined,
      }),
    enabled: Boolean(projectId),
  });
}

/**
 * Applies the active company's inclusion criteria (SISLAB-02) to a batch's animals, marking each
 * included/excluded from its readings. Returns how many animals a decision was taken for. Invalidates the batch
 * selection so the board refreshes.
 */
export function useApplySelection(projectId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      api.post<number>(Endpoints.projects.applySelection(projectId, batchId), {}),
    onSuccess: () =>
      queryClient.invalidateQueries({
        queryKey: [...projectKeys.all, 'selection', projectId, batchId],
      }),
  });
}

/**
 * A batch's animals with their inclusion decision (SISLAB-02) — the deciding value and reason — optionally
 * filtered by inclusion status ("Included"/"Excluded"). A blank status lists every animal.
 */
export function useSelection(projectId: string, batchId: string, status?: string) {
  const normalized = status?.trim() || null;
  return useQuery({
    queryKey: projectKeys.selection(projectId, batchId, normalized),
    queryFn: () =>
      api.get<AnimalSelectionListItem[]>(
        Endpoints.projects.selection(projectId, batchId),
        {
          status: normalized || undefined,
        },
      ),
    enabled: Boolean(projectId) && Boolean(batchId),
  });
}

function invalidateProject(
  queryClient: ReturnType<typeof useQueryClient>,
  projectId: string,
): void {
  void queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId) });
  void queryClient.invalidateQueries({ queryKey: projectKeys.all });
}
