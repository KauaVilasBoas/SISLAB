import { useState } from 'react';
import { AlertTriangle } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { useRooms, useCreateBooking } from '@/modules/agenda/api/rooms.queries';
import { ACTIVITY_LABEL } from '@/modules/agenda/presentation';
import type { AgendaActivity } from '@/modules/agenda/types';

const ACTIVITIES = Object.entries(ACTIVITY_LABEL) as [AgendaActivity, string][];

interface Props {
  date: string;
  onClose: () => void;
  onCreated: () => void;
}

export function CreateBookingModal({ date, onClose, onCreated }: Props) {
  const { data: rooms = [] } = useRooms();
  const createMutation = useCreateBooking();

  const [form, setForm] = useState({
    roomId: '',
    activity: 'VonFrey' as AgendaActivity,
    startTime: '08:00',
    endTime: '09:00',
    notes: '',
  });

  const [conflict, setConflict] = useState(false);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    createMutation.mutate(
      { ...form, date },
      {
        onSuccess: (result) => {
          if (result.conflictWarning) {
            setConflict(true);
            onCreated();
          } else {
            onCreated();
          }
        },
      },
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-xl bg-card p-6 shadow-lg">
        <h2 className="mb-4 text-lg font-semibold">Nova reserva — {date}</h2>

        {conflict && (
          <div className="mb-4 flex items-center gap-2 rounded-lg border border-status-warning/50 bg-status-warning/10 px-3 py-2 text-sm text-status-warning">
            <AlertTriangle className="size-4 shrink-0" />
            Reserva criada com aviso de conflito de horário.
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium">Sala</label>
            <select
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={form.roomId}
              onChange={(e) => setForm((f) => ({ ...f, roomId: e.target.value }))}
              required
            >
              <option value="">Selecione…</option>
              {rooms.map((r) => (
                <option key={r.id} value={r.id}>{r.name}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Atividade</label>
            <select
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={form.activity}
              onChange={(e) => setForm((f) => ({ ...f, activity: e.target.value as AgendaActivity }))}
            >
              {ACTIVITIES.map(([val, label]) => (
                <option key={val} value={val}>{label}</option>
              ))}
            </select>
          </div>

          <div className="flex gap-3">
            <div className="flex-1">
              <label className="mb-1 block text-sm font-medium">Início</label>
              <input
                type="time"
                className="w-full rounded-md border px-3 py-2 text-sm"
                value={form.startTime}
                onChange={(e) => setForm((f) => ({ ...f, startTime: e.target.value }))}
                required
              />
            </div>
            <div className="flex-1">
              <label className="mb-1 block text-sm font-medium">Fim</label>
              <input
                type="time"
                className="w-full rounded-md border px-3 py-2 text-sm"
                value={form.endTime}
                onChange={(e) => setForm((f) => ({ ...f, endTime: e.target.value }))}
                required
              />
            </div>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Observações (opcional)</label>
            <textarea
              className="w-full rounded-md border px-3 py-2 text-sm"
              rows={2}
              value={form.notes}
              onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
            />
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <Button type="button" variant="outline" onClick={onClose}>Cancelar</Button>
            <Button type="submit" disabled={createMutation.isPending}>
              {createMutation.isPending ? 'Reservando…' : 'Reservar'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
