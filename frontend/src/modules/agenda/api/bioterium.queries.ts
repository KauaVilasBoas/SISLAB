import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { BioteriumAssignmentItem } from '@/modules/agenda/types';

export const bioteriumKeys = {
  schedule: (from: string, to: string) => ['agenda', 'bioterium', from, to] as const,
};

export function useBioteriumSchedule(from: string, to: string) {
  return useQuery({
    queryKey: bioteriumKeys.schedule(from, to),
    queryFn: () =>
      api.get<BioteriumAssignmentItem[]>(Endpoints.agenda.bioterium, { from, to }),
    enabled: !!from && !!to,
  });
}

export function useGenerateBioteriumWeek() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { mondayOfWeek: string; responsibleNames: string[] }) =>
      api.post<void>(Endpoints.agenda.generateWeek, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['agenda', 'bioterium'] }),
  });
}

export function useSwapAssignment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assignmentId, newResponsibleName, reason }: {
      assignmentId: string;
      newResponsibleName: string;
      reason?: string;
    }) =>
      api.post<void>(Endpoints.agenda.swapAssignment(assignmentId), {
        newResponsibleName,
        reason,
      }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['agenda', 'bioterium'] }),
  });
}

export function useMarkDone() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ assignmentId, notes }: { assignmentId: string; notes?: string }) =>
      api.post<void>(Endpoints.agenda.markDone(assignmentId), { notes }),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['agenda', 'bioterium'] }),
  });
}
