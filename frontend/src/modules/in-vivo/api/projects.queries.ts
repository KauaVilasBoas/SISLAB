import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  AddAnimalRequest,
  AddGroupRequest,
  CreateProjectRequest,
  ProjectDetail,
  ProjectListItem,
} from '@/modules/in-vivo/types';

/** In vivo project query keys, namespaced so a write invalidates only this module's lists/details. */
export const projectKeys = {
  all: ['in-vivo', 'projects'] as const,
  list: (params: ListProjectsParams) => [...projectKeys.all, 'list', params] as const,
  detail: (id: string) => [...projectKeys.all, 'detail', id] as const,
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

/** Enrols an animal into a group. Invalidates the project detail + the list (animal count). */
export function useAddAnimal(projectId: string, batchId: string, groupId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AddAnimalRequest) =>
      api.post<string>(Endpoints.projects.animals(projectId, batchId, groupId), body),
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

function invalidateProject(
  queryClient: ReturnType<typeof useQueryClient>,
  projectId: string,
): void {
  void queryClient.invalidateQueries({ queryKey: projectKeys.detail(projectId) });
  void queryClient.invalidateQueries({ queryKey: projectKeys.all });
}
