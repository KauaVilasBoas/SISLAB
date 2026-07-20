import { useMemo, useState } from 'react';
import { Modal } from '@/shared/components/ui/modal';
import { Button } from '@/shared/components/ui/button';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import { cn } from '@/shared/lib/utils';
import {
  type CustomRecurrence,
  type EndMode,
  type Frequency,
  type Weekday,
  WEEKDAYS,
  WEEKDAY_LABEL,
  dayOfMonth,
  fromRRule,
  toRRule,
  weekdayOf,
} from '@/modules/agenda/lib/recurrence';

/**
 * Google-Calendar-style recurrence picker (card [E10.6]). A preset dropdown covers the common cases and a
 * "Custom…" option opens a modal to build an arbitrary RRULE. The component owns only the string RRULE (or null
 * for "does not repeat"); the anchor date drives the day-aware preset labels (weekly-on-day, monthly-on-day-N).
 */
interface RecurrenceEditorProps {
  /** The entry's start date 'YYYY-MM-DD' — anchors the weekly/monthly presets. */
  anchorDate: string;
  /** Current RRULE string, or null when the entry does not repeat. */
  value: string | null;
  onChange: (rrule: string | null) => void;
}

type PresetKey = 'none' | 'daily' | 'weekly' | 'weekday' | 'monthly' | 'annually' | 'custom';

export function RecurrenceEditor({ anchorDate, value, onChange }: RecurrenceEditorProps) {
  const [customOpen, setCustomOpen] = useState(false);

  const weekday = weekdayOf(anchorDate);
  const monthDay = dayOfMonth(anchorDate);

  const presets = useMemo(
    () => buildPresetRRules(weekday, monthDay),
    [weekday, monthDay],
  );

  // Which preset matches the current value? Anything unrecognised is "custom".
  const activePreset: PresetKey =
    value === null
      ? 'none'
      : (Object.keys(presets) as PresetKey[]).find((key) => presets[key] === value) ?? 'custom';

  function handleSelect(key: PresetKey) {
    if (key === 'custom') {
      setCustomOpen(true);
      return;
    }
    onChange(key === 'none' ? null : presets[key]);
  }

  return (
    <div className="space-y-1.5">
      <Label htmlFor="recurrence">Repetição</Label>
      <select
        id="recurrence"
        value={activePreset}
        onChange={(e) => handleSelect(e.target.value as PresetKey)}
        className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
      >
        <option value="none">Não se repete</option>
        <option value="daily">Diariamente</option>
        <option value="weekly">Semanalmente na {WEEKDAY_LABEL[weekday]}</option>
        <option value="weekday">Todo dia útil (seg–sex)</option>
        <option value="monthly">Mensalmente no dia {monthDay}</option>
        <option value="annually">Anualmente</option>
        <option value="custom">Personalizado…</option>
      </select>

      {activePreset === 'custom' && value && (
        <p className="text-xs text-muted-foreground">Regra personalizada: {value}</p>
      )}

      <CustomRecurrenceModal
        open={customOpen}
        onClose={() => setCustomOpen(false)}
        anchorWeekday={weekday}
        initial={fromRRule(value)}
        onSave={(recurrence) => {
          onChange(toRRule(recurrence));
          setCustomOpen(false);
        }}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Preset RRULEs
// ---------------------------------------------------------------------------

function buildPresetRRules(weekday: Weekday, monthDay: number): Record<PresetKey, string> {
  return {
    none: '',
    daily: 'FREQ=DAILY',
    weekly: `FREQ=WEEKLY;BYDAY=${weekday}`,
    weekday: 'FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR',
    monthly: `FREQ=MONTHLY;BYMONTHDAY=${monthDay}`,
    annually: 'FREQ=YEARLY',
    custom: '',
  };
}

// ---------------------------------------------------------------------------
// Custom builder modal
// ---------------------------------------------------------------------------

const FREQUENCY_UNIT: Record<Frequency, string> = {
  DAILY: 'dia(s)',
  WEEKLY: 'semana(s)',
  MONTHLY: 'mês(es)',
  YEARLY: 'ano(s)',
};

interface CustomRecurrenceModalProps {
  open: boolean;
  onClose: () => void;
  anchorWeekday: Weekday;
  initial: CustomRecurrence | null;
  onSave: (recurrence: CustomRecurrence) => void;
}

function CustomRecurrenceModal({
  open,
  onClose,
  anchorWeekday,
  initial,
  onSave,
}: CustomRecurrenceModalProps) {
  const [frequency, setFrequency] = useState<Frequency>(initial?.frequency ?? 'WEEKLY');
  const [interval, setInterval] = useState(initial?.interval ?? 1);
  const [byDay, setByDay] = useState<Weekday[]>(initial?.byDay ?? [anchorWeekday]);
  const [endMode, setEndMode] = useState<EndMode>(initial?.endMode ?? 'never');
  const [until, setUntil] = useState(initial?.until ?? '');
  const [count, setCount] = useState(initial?.count ?? 1);

  function toggleDay(day: Weekday) {
    setByDay((current) =>
      current.includes(day) ? current.filter((d) => d !== day) : [...current, day],
    );
  }

  function handleSave() {
    onSave({
      frequency,
      interval: Math.max(1, interval),
      byDay: frequency === 'WEEKLY' ? byDay : [],
      endMode,
      until: endMode === 'onDate' ? until || null : null,
      count: endMode === 'afterCount' ? Math.max(1, count) : null,
    });
  }

  return (
    <Modal
      open={open}
      onClose={onClose}
      title="Recorrência personalizada"
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button onClick={handleSave} disabled={endMode === 'onDate' && !until}>
            Concluído
          </Button>
        </>
      }
    >
      <div className="space-y-5">
        <div className="flex items-end gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="interval">Repetir a cada</Label>
            <Input
              id="interval"
              type="number"
              min={1}
              value={interval}
              onChange={(e) => setInterval(Number(e.target.value))}
              className="w-20"
            />
          </div>
          <div className="flex-1 space-y-1.5">
            <Label htmlFor="frequency">Unidade</Label>
            <select
              id="frequency"
              value={frequency}
              onChange={(e) => setFrequency(e.target.value as Frequency)}
              className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            >
              {(Object.keys(FREQUENCY_UNIT) as Frequency[]).map((f) => (
                <option key={f} value={f}>
                  {FREQUENCY_UNIT[f]}
                </option>
              ))}
            </select>
          </div>
        </div>

        {frequency === 'WEEKLY' && (
          <div className="space-y-1.5">
            <Label>Repetir em</Label>
            <div className="flex flex-wrap gap-1.5">
              {WEEKDAYS.map((day) => (
                <button
                  key={day}
                  type="button"
                  onClick={() => toggleDay(day)}
                  aria-pressed={byDay.includes(day)}
                  className={cn(
                    'size-9 rounded-full text-xs font-medium transition-colors',
                    byDay.includes(day)
                      ? 'bg-primary text-primary-foreground'
                      : 'bg-muted text-muted-foreground hover:bg-accent',
                  )}
                >
                  {WEEKDAY_LABEL[day]}
                </button>
              ))}
            </div>
          </div>
        )}

        <fieldset className="space-y-2">
          <legend className="text-sm font-medium">Termina</legend>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="endMode"
              checked={endMode === 'never'}
              onChange={() => setEndMode('never')}
            />
            Nunca
          </label>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="endMode"
              checked={endMode === 'onDate'}
              onChange={() => setEndMode('onDate')}
            />
            Em
            <Input
              type="date"
              value={until}
              onChange={(e) => setUntil(e.target.value)}
              disabled={endMode !== 'onDate'}
              className="w-40"
            />
          </label>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="radio"
              name="endMode"
              checked={endMode === 'afterCount'}
              onChange={() => setEndMode('afterCount')}
            />
            Após
            <Input
              type="number"
              min={1}
              value={count}
              onChange={(e) => setCount(Number(e.target.value))}
              disabled={endMode !== 'afterCount'}
              className="w-20"
            />
            ocorrência(s)
          </label>
        </fieldset>
      </div>
    </Modal>
  );
}
