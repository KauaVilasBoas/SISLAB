import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  BookingListItem,
  CreateBookingResponse,
  RoomListItem,
  AgendaActivity,
  RoomType,
} from '@/modules/agenda/types';

export const roomKeys = {
  all: ['agenda', 'rooms'] as const,
  calendar: (date: string) => ['agenda', 'calendar', date] as const,
};

export function useRooms() {
  return useQuery({
    queryKey: roomKeys.all,
    queryFn: () => api.get<RoomListItem[]>(Endpoints.agenda.rooms),
  });
}

export function useCalendar(date: string) {
  return useQuery({
    queryKey: roomKeys.calendar(date),
    queryFn: () =>
      api.get<BookingListItem[]>(Endpoints.agenda.calendar, { date }),
    enabled: !!date,
  });
}

export function useCreateBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {
      roomId: string;
      activity: AgendaActivity;
      date: string;
      startTime: string;
      endTime: string;
      notes?: string;
    }) => api.post<CreateBookingResponse>(Endpoints.agenda.bookings, body),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: roomKeys.calendar(variables.date) });
    },
  });
}

export function useRegisterRoom() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: { name: string; capacity: number; type: RoomType }) =>
      api.post<string>(Endpoints.agenda.rooms, body),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: roomKeys.all }),
  });
}

export function useCancelBooking() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ bookingId }: { bookingId: string; date: string }) =>
      api.del<void>(Endpoints.agenda.cancelBooking(bookingId)),
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: roomKeys.calendar(variables.date) });
    },
  });
}
