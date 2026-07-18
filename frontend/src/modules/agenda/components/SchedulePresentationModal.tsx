import { useState } from 'react';
import { Button } from '@/shared/components/ui/button';
import { useSchedulePresentation } from '@/modules/agenda/api/presentations.queries';
import { PRESENTATION_TYPE_LABEL } from '@/modules/agenda/presentation';
import type { PresentationType } from '@/modules/agenda/types';

const TYPES = Object.entries(PRESENTATION_TYPE_LABEL) as [PresentationType, string][];

interface Props {
  onClose: () => void;
  onScheduled: () => void;
}

export function SchedulePresentationModal({ onClose, onScheduled }: Props) {
  const scheduleMutation = useSchedulePresentation();

  const [form, setForm] = useState({
    type: 'Article' as PresentationType,
    title: '',
    doi: '',
    presenterName: '',
    scheduledDate: '',
    notes: '',
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    scheduleMutation.mutate(
      {
        type: form.type,
        title: form.title,
        doi: form.doi || undefined,
        presenterName: form.presenterName,
        scheduledDate: form.scheduledDate,
        notes: form.notes || undefined,
      },
      { onSuccess: onScheduled },
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="w-full max-w-md rounded-xl bg-card p-6 shadow-lg">
        <h2 className="mb-4 text-lg font-semibold">Agendar apresentação</h2>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="mb-1 block text-sm font-medium">Tipo</label>
            <select
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={form.type}
              onChange={(e) => setForm((f) => ({ ...f, type: e.target.value as PresentationType }))}
            >
              {TYPES.map(([val, label]) => (
                <option key={val} value={val}>{label}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Título</label>
            <input
              type="text"
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={form.title}
              onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">DOI (opcional)</label>
            <input
              type="text"
              className="w-full rounded-md border px-3 py-2 text-sm"
              placeholder="10.xxxx/xxxxx"
              value={form.doi}
              onChange={(e) => setForm((f) => ({ ...f, doi: e.target.value }))}
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Apresentador</label>
            <input
              type="text"
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={form.presenterName}
              onChange={(e) => setForm((f) => ({ ...f, presenterName: e.target.value }))}
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-sm font-medium">Data</label>
            <input
              type="date"
              className="w-full rounded-md border px-3 py-2 text-sm"
              value={form.scheduledDate}
              onChange={(e) => setForm((f) => ({ ...f, scheduledDate: e.target.value }))}
              required
            />
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
            <Button type="submit" disabled={scheduleMutation.isPending}>
              {scheduleMutation.isPending ? 'Agendando…' : 'Agendar'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
