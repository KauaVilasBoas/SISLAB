import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PresentationListItem, PresentationType } from '@/modules/agenda/types';

export const presentationKeys = {
  schedule: (from: string, to: string) => ['agenda', 'presentations', from, to] as const,
};

export function usePresentationSchedule(from: string, to: string) {
  return useQuery({
    queryKey: presentationKeys.schedule(from, to),
    queryFn: () =>
      api.get<PresentationListItem[]>(Endpoints.agenda.presentations, { from, to }),
    enabled: !!from && !!to,
  });
}

export function useSchedulePresentation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      type: PresentationType;
      title: string;
      doi?: string;
      presenterName: string;
      scheduledDate: string;
      notes?: string;
    }) => api.post<string>(Endpoints.agenda.presentations, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['agenda', 'presentations'] }),
  });
}

export function useCancelPresentation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (presentationId: string) =>
      api.del<void>(Endpoints.agenda.cancelPresentation(presentationId)),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ['agenda', 'presentations'] }),
  });
}
