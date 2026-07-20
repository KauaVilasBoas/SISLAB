import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type { PagedResult } from '@/shared/types/api';
import type {
  AgendaEntryMutationResult,
  CalendarItem,
  CreateAgendaEntryRequest,
  RoomOccupancySlot,
  UpdateAgendaEntryRequest,
} from '@/modules/agenda/types';
import type { ExperimentListItem } from '@/modules/experiments/types';

/**
 * Query/mutation layer for the improved calendar (cards [E10.5-7]) over the unified AgendaEntry API. Keys are
 * namespaced under 'agenda','entries' so a write invalidates only the calendar without touching the legacy
 * room/bioterium/presentation lists.
 */
export const entryKeys = {
  all: ['agenda', 'entries'] as const,
  calendar: (params: CalendarQueryParams) => ['agenda', 'entries', 'calendar', params] as const,
  occupancy: (date: string) => ['agenda', 'entries', 'occupancy', date] as const,
};

export interface CalendarFilters {
  activityType?: string;
  responsibleId?: string;
  experimentId?: string;
  onlyMine?: boolean;
}

export interface CalendarQueryParams extends CalendarFilters {
  start: string;
  end: string;
}

/** Every calendar occurrence in the inclusive [start, end] range, honouring the opt-in filters. */
export function useCalendarEntries(params: CalendarQueryParams) {
  return useQuery({
    queryKey: entryKeys.calendar(params),
    queryFn: () =>
      api.get<CalendarItem[]>(Endpoints.agenda.entriesCalendar, {
        start: params.start,
        end: params.end,
        activityType: params.activityType || undefined,
        responsibleId: params.responsibleId || undefined,
        experimentId: params.experimentId || undefined,
        onlyMine: params.onlyMine || undefined,
      }),
    enabled: !!params.start && !!params.end,
  });
}

/** Room-occupancy Gantt slots for a single day (card [E10.11]). */
export function useRoomOccupancy(date: string) {
  return useQuery({
    queryKey: entryKeys.occupancy(date),
    queryFn: () => api.get<RoomOccupancySlot[]>(Endpoints.agenda.roomOccupancy, { date }),
    enabled: !!date,
  });
}

export function useCreateEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateAgendaEntryRequest) =>
      api.post<AgendaEntryMutationResult>(Endpoints.agenda.entries, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: entryKeys.all }),
  });
}

export function useUpdateEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UpdateAgendaEntryRequest }) =>
      api.put<AgendaEntryMutationResult>(Endpoints.agenda.entry(id), body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: entryKeys.all }),
  });
}

export function useDeleteEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.del<void>(Endpoints.agenda.entry(id)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: entryKeys.all }),
  });
}

/** Cancels a single occurrence of a recurring entry (adds an EXDATE). */
export function useCancelOccurrence() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, date }: { id: string; date: string }) =>
      api.del<void>(Endpoints.agenda.cancelOccurrence(id, date)),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: entryKeys.all }),
  });
}

/** Generates/renews the current user's iCal feed token (card [E10.10]). */
export function useSubscribeIcal() {
  return useMutation({
    mutationFn: () => api.post<{ token: string }>(Endpoints.agenda.icalSubscribe),
  });
}

/** Lightweight experiment options for the entry form's nullable autocomplete. */
export function useExperimentOptions(search: string) {
  return useQuery({
    queryKey: ['agenda', 'experiment-options', search] as const,
    queryFn: async () => {
      const page = await api.get<PagedResult<ExperimentListItem>>(Endpoints.experiments.root, {
        page: 1,
        pageSize: 20,
      });
      const term = search.trim().toLowerCase();
      const items = term
        ? page.items.filter((e) => e.title.toLowerCase().includes(term))
        : page.items;
      return items.map((e) => ({ id: e.id, title: e.title }));
    },
  });
}
