import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  AssignCollectionRoleRequest,
  CollectionPlanView,
  CollectionStatusBoardView,
  CreateCollectionPlanRequest,
  DefineSampleRoutingRequest,
} from '@/modules/in-vivo/types';

/**
 * Collection-plan query keys (SISLAB-08), namespaced so a plan write invalidates only this batch's plan and its
 * derived status board (the board is recomputed from the real biobank state, so it is refreshed alongside).
 */
export const collectionKeys = {
  all: ['in-vivo', 'collection'] as const,
  plan: (batchId: string) => [...collectionKeys.all, 'plan', batchId] as const,
  status: (batchId: string) => [...collectionKeys.all, 'status', batchId] as const,
};

/**
 * A batch's collection plan (matrix + role roster). The GET returns 404 while no plan exists yet — the caller
 * treats that as "sem plano" (offer to create), so failures are not retried into a spurious error state.
 */
export function useCollectionPlan(batchId: string, enabled = true) {
  return useQuery({
    queryKey: collectionKeys.plan(batchId),
    queryFn: () =>
      api.get<CollectionPlanView>(Endpoints.collectionPlans.byBatch(batchId)),
    enabled: enabled && Boolean(batchId),
    retry: false,
  });
}

/** A batch's derived status board (pending/done per planned analysis). Only meaningful once a plan exists. */
export function useCollectionStatusBoard(batchId: string, enabled = true) {
  return useQuery({
    queryKey: collectionKeys.status(batchId),
    queryFn: () =>
      api.get<CollectionStatusBoardView>(Endpoints.collectionPlans.status(batchId)),
    enabled: enabled && Boolean(batchId),
    retry: false,
  });
}

/** Creates the collection plan for a batch; returns the new id. Invalidates the batch's plan + status board. */
export function useCreateCollectionPlan(batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateCollectionPlanRequest) =>
      api.post<string>(Endpoints.collectionPlans.root, body),
    onSuccess: () => invalidateBatch(queryClient, batchId),
  });
}

/** Defines (or replaces) a sample type's routing on a plan. Invalidates the batch's plan + status board. */
export function useDefineSampleRouting(planId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: DefineSampleRoutingRequest) =>
      api.put<void>(Endpoints.collectionPlans.routings(planId), body),
    onSuccess: () => invalidateBatch(queryClient, batchId),
  });
}

/** Removes a sample type's routing from a plan (sample type by name). Invalidates the batch's plan + status board. */
export function useRemoveSampleRouting(planId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (sampleType: string) =>
      api.del<void>(Endpoints.collectionPlans.routing(planId, sampleType)),
    onSuccess: () => invalidateBatch(queryClient, batchId),
  });
}

/** Assigns a member to a collection role on a plan. Invalidates the batch's plan + status board. */
export function useAssignCollectionRole(planId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: AssignCollectionRoleRequest) =>
      api.put<void>(Endpoints.collectionPlans.roles(planId), body),
    onSuccess: () => invalidateBatch(queryClient, batchId),
  });
}

/** Removes a role assignment from a plan. Invalidates the batch's plan + status board. */
export function useRemoveCollectionRole(planId: string, batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (roleId: string) =>
      api.del<void>(Endpoints.collectionPlans.role(planId, roleId)),
    onSuccess: () => invalidateBatch(queryClient, batchId),
  });
}

function invalidateBatch(
  queryClient: ReturnType<typeof useQueryClient>,
  batchId: string,
): void {
  void queryClient.invalidateQueries({ queryKey: collectionKeys.plan(batchId) });
  void queryClient.invalidateQueries({ queryKey: collectionKeys.status(batchId) });
}
