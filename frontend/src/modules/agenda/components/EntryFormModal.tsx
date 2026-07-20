import { useMemo, useState } from 'react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { useToast } from '@/shared/components/ui/toast';
import { ACTIVITY_TYPE_LABEL } from '@/modules/agenda/presentation';
import { RecurrenceEditor } from '@/modules/agenda/components/RecurrenceEditor';
import { EntryColorPicker } from '@/modules/agenda/components/EntryColorPicker';
import { ExperimentSelect } from '@/modules/agenda/components/ExperimentSelect';
import { EditRecurringDialog } from '@/modules/agenda/components/EditRecurringDialog';
import { useCreateEntry, useUpdateEntry } from '@/modules/agenda/api/entries.queries';
import { useRooms } from '@/modules/agenda/api/rooms.queries';
import type {
  AgendaActivityType,
  AgendaConflictWarning,
  CalendarItem,
  EditScope,
} from '@/modules/agenda/types';

/**
 * Create/edit entry form (card [E10.6]) with Google-Calendar UX: title, description, start/end, an "all day"
 * toggle that hides the time fields, an activity type, a nullable experiment link and a recurrence picker.
 * Editing a recurring entry first asks for the edit scope (only-this / this-and-following / all).
 */
interface EntryFormModalProps {
  open: boolean;
  onClose: () => void;
  /** The occurrence being edited, or null to create. Prefilled start date comes from `defaultDate` on create. */
  editing: CalendarItem | null;
  /** 'YYYY-MM-DD' to seed a new entry (the day the user clicked). */
  defaultDate: string;
}

const ACTIVITY_TYPES: AgendaActivityType[] = [
  'RoomBooking',
  'Experiment',
  'Bioterium',
  'Presentation',
  'Other',
];

const WARNING_LABEL: Record<AgendaConflictWarning, string> = {
  conflict_person: 'O responsável já tem outro compromisso nesse horário.',
  conflict_room: 'Há outra reserva de sala sobreposta.',
};

// ---- datetime-local <-> UTC ISO helpers ----------------------------------

/** 'YYYY-MM-DDTHH:mm' (local) for a datetime-local input from a UTC ISO instant. */
function toLocalInput(isoUtc: string): string {
  const d = new Date(isoUtc);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** UTC ISO instant from a local 'YYYY-MM-DDTHH:mm' datetime-local value. */
function toUtcIso(localValue: string): string {
  return new Date(localValue).toISOString();
}

/** UTC ISO at local midnight of a 'YYYY-MM-DD' (all-day start). */
function dayStartUtc(isoDate: string): string {
  const [y, m, d] = isoDate.split('-').map(Number);
  return new Date(y, m - 1, d, 0, 0, 0).toISOString();
}

export function EntryFormModal({ open, onClose, editing, defaultDate }: EntryFormModalProps) {
  const toast = useToast();
  const createEntry = useCreateEntry();
  const updateEntry = useUpdateEntry();

  const initial = useMemo(() => buildInitialState(editing, defaultDate), [editing, defaultDate]);

  const [title, setTitle] = useState(initial.title);
  const [description, setDescription] = useState(initial.description);
  const [isAllDay, setIsAllDay] = useState(initial.isAllDay);
  const [startDate, setStartDate] = useState(initial.startDate);
  const [endDate, setEndDate] = useState(initial.endDate);
  const [startDateTime, setStartDateTime] = useState(initial.startDateTime);
  const [endDateTime, setEndDateTime] = useState(initial.endDateTime);
  const [activityType, setActivityType] = useState<AgendaActivityType>(initial.activityType);
  const [experimentId, setExperimentId] = useState<string | null>(initial.experimentId);
  const [experimentLabel, setExperimentLabel] = useState<string | null>(initial.experimentLabel);
  const [roomId, setRoomId] = useState<string | null>(initial.roomId);
  const [recurrenceRule, setRecurrenceRule] = useState<string | null>(initial.recurrenceRule);
  const [color, setColor] = useState<string | null>(initial.color);

  // Rooms only matter for a RoomBooking; load them lazily and let the operator pick which room the entry occupies.
  const rooms = useRooms();

  const [scopeDialogOpen, setScopeDialogOpen] = useState(false);

  const isEditing = editing !== null;
  const isSaving = createEntry.isPending || updateEntry.isPending;
  const anchorDate = isAllDay ? startDate : startDateTime.slice(0, 10);

  function buildPayload() {
    const start = isAllDay ? dayStartUtc(startDate) : toUtcIso(startDateTime);
    const end = isAllDay ? dayStartUtc(endDate) : toUtcIso(endDateTime);
    return {
      title: title.trim(),
      description: description.trim() || null,
      startDateUtc: start,
      endDateUtc: end,
      isAllDay,
      activityType,
      experimentId,
      // A room only belongs to a RoomBooking; the backend also normalises this, but keep the payload honest.
      roomId: activityType === 'RoomBooking' ? roomId : null,
      recurrenceRule,
      color,
    };
  }

  function reportWarnings(warnings: AgendaConflictWarning[]) {
    for (const warning of warnings) toast('error', WARNING_LABEL[warning]);
  }

  function handleSubmit() {
    if (!title.trim()) {
      toast('error', 'Informe um título.');
      return;
    }

    // Editing a recurring entry must first choose the scope.
    if (isEditing && editing.isRecurring) {
      setScopeDialogOpen(true);
      return;
    }

    if (isEditing) {
      saveUpdate('AllOccurrences');
    } else {
      createEntry.mutate(buildPayload(), {
        onSuccess: (result) => {
          toast('success', 'Evento criado.');
          reportWarnings(result.warnings);
          onClose();
        },
        onError: () => toast('error', 'Não foi possível criar o evento.'),
      });
    }
  }

  function saveUpdate(scope: EditScope) {
    if (!editing) return;
    setScopeDialogOpen(false);
    updateEntry.mutate(
      {
        id: editing.id,
        body: {
          editScope: scope,
          occurrenceDate: editing.occurrenceDate,
          ...buildPayload(),
        },
      },
      {
        onSuccess: (result) => {
          toast('success', 'Evento atualizado.');
          reportWarnings(result.warnings);
          onClose();
        },
        onError: () => toast('error', 'Não foi possível atualizar o evento.'),
      },
    );
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="lg"
      title={isEditing ? 'Editar evento' : 'Novo evento'}
      footer={
        <>
          <Button variant="ghost" onClick={onClose} disabled={isSaving}>
            Cancelar
          </Button>
          <Button onClick={handleSubmit} disabled={isSaving}>
            {isSaving ? 'Salvando…' : 'Salvar'}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <div className="space-y-1.5">
          <Label htmlFor="title">Título</Label>
          <div className="flex items-center gap-2">
            <EntryColorPicker value={color} onChange={setColor} />
            <Input
              id="title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              autoFocus
              className="flex-1"
            />
          </div>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="description">Descrição</Label>
          <textarea
            id="description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={2}
            className="flex w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={isAllDay}
            onChange={(e) => setIsAllDay(e.target.checked)}
          />
          Dia inteiro
        </label>

        {isAllDay ? (
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="startDate">Início</Label>
              <Input
                id="startDate"
                type="date"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="endDate">Fim</Label>
              <Input
                id="endDate"
                type="date"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
              />
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="startDateTime">Início</Label>
              <Input
                id="startDateTime"
                type="datetime-local"
                value={startDateTime}
                onChange={(e) => setStartDateTime(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="endDateTime">Fim</Label>
              <Input
                id="endDateTime"
                type="datetime-local"
                value={endDateTime}
                onChange={(e) => setEndDateTime(e.target.value)}
              />
            </div>
          </div>
        )}

        <div className="space-y-1.5">
          <Label htmlFor="activityType">Tipo</Label>
          <select
            id="activityType"
            value={activityType}
            onChange={(e) => setActivityType(e.target.value as AgendaActivityType)}
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            {ACTIVITY_TYPES.map((type) => (
              <option key={type} value={type}>
                {ACTIVITY_TYPE_LABEL[type]}
              </option>
            ))}
          </select>
        </div>

        {activityType === 'RoomBooking' && (
          <div className="space-y-1.5">
            <Label htmlFor="roomId">Sala</Label>
            <select
              id="roomId"
              value={roomId ?? ''}
              onChange={(e) => setRoomId(e.target.value || null)}
              className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              <option value="">Sem sala</option>
              {(rooms.data ?? []).map((room) => (
                <option key={room.id} value={room.id}>
                  {room.name}
                </option>
              ))}
            </select>
          </div>
        )}

        <ExperimentSelect
          value={experimentId}
          valueLabel={experimentLabel}
          onChange={(id, label) => {
            setExperimentId(id);
            setExperimentLabel(label);
          }}
        />

        <RecurrenceEditor
          anchorDate={anchorDate}
          value={recurrenceRule}
          onChange={setRecurrenceRule}
        />
      </div>

      <EditRecurringDialog
        open={scopeDialogOpen}
        onClose={() => setScopeDialogOpen(false)}
        onChoose={saveUpdate}
      />
    </Modal>
  );
}

// ---------------------------------------------------------------------------
// Initial form state
// ---------------------------------------------------------------------------

function buildInitialState(editing: CalendarItem | null, defaultDate: string) {
  if (editing) {
    return {
      title: editing.title,
      description: '',
      isAllDay: editing.isAllDay,
      startDate: editing.occurrenceDate,
      endDate: editing.occurrenceDate,
      startDateTime: toLocalInput(editing.startDateUtc),
      endDateTime: toLocalInput(editing.endDateUtc),
      activityType: editing.activityType,
      experimentId: editing.experimentId,
      experimentLabel: editing.experimentName,
      roomId: editing.roomId,
      // Pre-populate the recurrence editor with the series' raw RRULE so editing a recurring entry no longer
      // opens with an empty recurrence field (the RecurrenceEditor parses this string into its preset/custom UI).
      recurrenceRule: editing.recurrenceRule,
      color: editing.color,
    };
  }

  const defaultStart = `${defaultDate}T09:00`;
  const defaultEnd = `${defaultDate}T10:00`;
  return {
    title: '',
    description: '',
    isAllDay: false,
    startDate: defaultDate,
    endDate: defaultDate,
    startDateTime: defaultStart,
    endDateTime: defaultEnd,
    activityType: 'Other' as AgendaActivityType,
    experimentId: null as string | null,
    experimentLabel: null as string | null,
    roomId: null as string | null,
    recurrenceRule: null as string | null,
    color: null as string | null,
  };
}
