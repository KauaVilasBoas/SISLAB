import { useState } from 'react';
import { AlertTriangle, Calendar, Loader2, Plus } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { RequirePermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import { useCalendar, useCancelBooking } from '@/modules/agenda/api/rooms.queries';
import {
  ACTIVITY_LABEL,
  formatDate,
  formatTime,
  todayIso,
} from '@/modules/agenda/presentation';
import { CreateBookingModal } from '@/modules/agenda/components/CreateBookingModal';
import type { BookingListItem } from '@/modules/agenda/types';

/**
 * Room booking calendar page (card [E10] #69). Shows all active bookings for the selected date across
 * all rooms, with a permission-gated "New booking" action. Conflict-warned bookings are flagged visually.
 */
export function RoomBookingPage() {
  const [date, setDate] = useState(todayIso());
  const [creating, setCreating] = useState(false);

  const { data: bookings = [], isLoading } = useCalendar(date);
  const cancelMutation = useCancelBooking();

  function shiftDate(days: number) {
    const d = new Date(date);
    d.setDate(d.getDate() + days);
    setDate(d.toISOString().substring(0, 10));
  }

  function handleCancel(booking: BookingListItem) {
    if (!confirm(`Cancelar reserva de "${booking.roomName}" às ${formatTime(booking.startTime)}?`)) return;
    cancelMutation.mutate({ bookingId: booking.bookingId, date });
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Agendamento de salas"
        description="Reservas por sala com detecção de conflito."
        actions={
          <RequirePermission code={Permissions.agenda.createBooking}>
            <Button onClick={() => setCreating(true)}>
              <Plus className="size-4" />
              Nova reserva
            </Button>
          </RequirePermission>
        }
      />

      <div className="flex items-center gap-3">
        <Button variant="outline" size="sm" onClick={() => shiftDate(-1)}>←</Button>
        <div className="flex items-center gap-2 text-sm font-medium">
          <Calendar className="size-4 text-muted-foreground" />
          {formatDate(date)}
        </div>
        <Button variant="outline" size="sm" onClick={() => shiftDate(1)}>→</Button>
        <Button variant="ghost" size="sm" onClick={() => setDate(todayIso())}>Hoje</Button>
        <input
          type="date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
          className="ml-auto h-8 rounded-md border px-2 text-sm"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Reservas do dia</CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" />
              Carregando…
            </div>
          ) : bookings.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Nenhuma reserva para este dia.
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="px-4 py-3 font-medium">Sala</th>
                  <th className="px-4 py-3 font-medium">Horário</th>
                  <th className="px-4 py-3 font-medium">Atividade</th>
                  <th className="px-4 py-3 font-medium">Reservado por</th>
                  <th className="px-4 py-3 font-medium"></th>
                </tr>
              </thead>
              <tbody>
                {bookings.map((b) => (
                  <tr key={b.bookingId} className="border-b last:border-0">
                    <td className="px-4 py-3 font-medium">
                      <span className="flex items-center gap-1.5">
                        {b.roomName}
                        {b.hasConflictWarning && (
                          <AlertTriangle className="size-4 text-status-warning" aria-label="Conflito de horário detectado" />
                        )}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatTime(b.startTime)} – {formatTime(b.endTime)}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {ACTIVITY_LABEL[b.activity] ?? b.activity}
                    </td>
                    <td className="px-4 py-3 text-muted-foreground">{b.bookedByName}</td>
                    <td className="px-4 py-3 text-right">
                      <RequirePermission code={Permissions.agenda.cancelBooking}>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleCancel(b)}
                          disabled={cancelMutation.isPending}
                        >
                          Cancelar
                        </Button>
                      </RequirePermission>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </CardContent>
      </Card>

      {creating && (
        <CreateBookingModal
          date={date}
          onClose={() => setCreating(false)}
          onCreated={() => setCreating(false)}
        />
      )}
    </div>
  );
}
