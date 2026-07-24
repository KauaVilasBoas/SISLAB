import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import { entryKeys } from '@/modules/agenda/api/entries.queries';
import type {
  GenerateScheduleRequest,
  GenerateScheduleResult,
} from '@/modules/in-vivo/types';

/**
 * Generates an experiment's schedule from its bound experimental model (SISLAB-10) and materialises the
 * resulting entries in the Agenda module, rotating the roster of responsibles across the days. The
 * `experimentId` is the batch (leva) whose model drives the cadence; the created entries carry it by value,
 * so the Agenda calendar can be filtered to exactly this run via `?experimentId=<batchId>`.
 *
 * On success the freshly created entries land on the calendar, so we invalidate the Agenda cache to reflect
 * them without a manual refresh. Returns the created entry ids (chronological) so the caller can report the
 * count and link straight to the filtered calendar.
 */
export function useGenerateSchedule(experimentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: GenerateScheduleRequest) =>
      api.post<GenerateScheduleResult>(Endpoints.experiments.schedule(experimentId), body),
    onSuccess: () => {
      // The new entries appear on the calendar/agenda views — refresh any cached calendar window.
      void queryClient.invalidateQueries({ queryKey: entryKeys.all });
    },
  });
}
