import { cn } from '@/shared/lib/utils';
import { ACTIVITY_TYPE_COLOR } from '@/modules/agenda/presentation';
import {
  addDays,
  localDateOf,
  localTime,
  parseIsoDate,
  todayIso,
  weekDays,
} from '@/modules/agenda/lib/calendar';
import type { CalendarItem } from '@/modules/agenda/types';

/** Shared props for every calendar view: the occurrences to render, the anchor day, and the click handler. */
interface CalendarViewProps {
  items: CalendarItem[];
  anchorDate: string;
  onSelect: (item: CalendarItem) => void;
  selectedId?: string | null;
  /** Called when an empty day cell is clicked — seeds "new event" on that day. */
  onCreateOn: (isoDate: string) => void;
}

// ---------------------------------------------------------------------------
// Shared helpers
// ---------------------------------------------------------------------------

function itemsOn(items: CalendarItem[], isoDate: string): CalendarItem[] {
  return items
    .filter((item) => localDateOf(item.startDateUtc) === isoDate)
    .sort((a, b) => a.startDateUtc.localeCompare(b.startDateUtc));
}

function EventChip({
  item,
  onSelect,
  selected,
  compact,
}: {
  item: CalendarItem;
  onSelect: (item: CalendarItem) => void;
  selected: boolean;
  compact?: boolean;
}) {
  const color = ACTIVITY_TYPE_COLOR[item.activityType];
  // A per-entry colour override (card [E10.12]) wins over the automatic activity-type palette: render a soft
  // tinted chip with the chosen hue via inline styles (Tailwind cannot take a runtime hex). When no override is
  // set, fall back to the activity-type utility classes.
  const custom = item.color;
  const customStyle = custom
    ? { backgroundColor: `${custom}1a`, borderColor: `${custom}66`, color: custom }
    : undefined;
  return (
    <button
      type="button"
      onClick={(e) => {
        e.stopPropagation();
        onSelect(item);
      }}
      className={cn(
        'flex w-full items-center gap-1 rounded border px-1.5 py-1 text-left text-xs transition-colors',
        !custom && [color.bg, color.border, color.text],
        selected && 'ring-2 ring-ring',
      )}
      style={customStyle}
    >
      <span
        className={cn('size-2 shrink-0 rounded-full', !custom && color.dot)}
        style={custom ? { backgroundColor: custom } : undefined}
      />
      {!item.isAllDay && !compact && (
        <span className="shrink-0 tabular-nums opacity-80">{localTime(item.startDateUtc)}</span>
      )}
      <span className="truncate font-medium">{item.title}</span>
    </button>
  );
}

function dayHeaderLabel(isoDate: string): { weekday: string; day: string } {
  const date = parseIsoDate(isoDate);
  return {
    weekday: date.toLocaleDateString('pt-BR', { weekday: 'short' }),
    day: String(date.getDate()),
  };
}

// ---------------------------------------------------------------------------
// Day view — a chronological list with a visual time rail
// ---------------------------------------------------------------------------

export function DayView({ items, anchorDate, onSelect, selectedId, onCreateOn }: CalendarViewProps) {
  const dayItems = itemsOn(items, anchorDate);
  const allDay = dayItems.filter((i) => i.isAllDay);
  const timed = dayItems.filter((i) => !i.isAllDay);

  return (
    <div className="rounded-lg border bg-card">
      {allDay.length > 0 && (
        <div className="space-y-1 border-b p-3">
          {allDay.map((item) => (
            <EventChip
              key={`${item.id}-${item.occurrenceDate}`}
              item={item}
              onSelect={onSelect}
              selected={selectedId === item.id}
            />
          ))}
        </div>
      )}

      {timed.length === 0 && allDay.length === 0 ? (
        <button
          type="button"
          onClick={() => onCreateOn(anchorDate)}
          className="flex w-full items-center justify-center py-16 text-sm text-muted-foreground hover:bg-accent/50"
        >
          Nenhum evento — clique para adicionar
        </button>
      ) : (
        <ul className="divide-y">
          {timed.map((item) => (
            <li key={`${item.id}-${item.occurrenceDate}`} className="flex items-stretch gap-3 p-3">
              <div className="w-16 shrink-0 pt-0.5 text-right text-xs tabular-nums text-muted-foreground">
                {localTime(item.startDateUtc)}
                <br />
                {localTime(item.endDateUtc)}
              </div>
              <div className="flex-1">
                <EventChip item={item} onSelect={onSelect} selected={selectedId === item.id} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Week view — seven day columns
// ---------------------------------------------------------------------------

export function WeekView({ items, anchorDate, onSelect, selectedId, onCreateOn }: CalendarViewProps) {
  const days = weekDays(anchorDate);
  const today = todayIso();

  return (
    <div className="grid grid-cols-7 gap-2">
      {days.map((day) => {
        const dayItems = itemsOn(items, day);
        const { weekday, day: dayNum } = dayHeaderLabel(day);
        const isToday = day === today;

        return (
          <div
            key={day}
            role="button"
            tabIndex={0}
            onClick={() => onCreateOn(day)}
            onKeyDown={(e) => e.key === 'Enter' && onCreateOn(day)}
            className="min-h-40 cursor-pointer rounded-lg border bg-card transition-colors hover:border-ring/50"
          >
            <div
              className={cn(
                'rounded-t-lg px-2 py-1.5 text-center text-xs font-semibold uppercase tracking-wide',
                isToday ? 'bg-primary text-primary-foreground' : 'text-muted-foreground',
              )}
            >
              {weekday} {dayNum}
            </div>
            <div className="space-y-1 p-1">
              {dayItems.length === 0 ? (
                <p className="py-2 text-center text-xs text-muted-foreground/40">—</p>
              ) : (
                dayItems.map((item) => (
                  <EventChip
                    key={`${item.id}-${item.occurrenceDate}`}
                    item={item}
                    onSelect={onSelect}
                    selected={selectedId === item.id}
                  />
                ))
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Month view — a classic 7-column grid padded to whole weeks
// ---------------------------------------------------------------------------

export function MonthView({ items, anchorDate, onSelect, selectedId, onCreateOn }: CalendarViewProps) {
  const anchorMonth = parseIsoDate(anchorDate).getMonth();
  const today = todayIso();

  // Build the padded grid: start from the Monday of the first week, span whole weeks.
  const { start, end } = monthRange(anchorDate);
  const cells: string[] = [];
  for (let day = start; day <= end; day = addDays(day, 1)) cells.push(day);

  const weekdayHeaders = ['Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];

  return (
    <div className="rounded-lg border bg-card">
      <div className="grid grid-cols-7 border-b text-center text-xs font-semibold uppercase tracking-wide text-muted-foreground">
        {weekdayHeaders.map((label) => (
          <div key={label} className="py-2">
            {label}
          </div>
        ))}
      </div>
      <div className="grid grid-cols-7">
        {cells.map((day) => {
          const dayItems = itemsOn(items, day);
          const inMonth = parseIsoDate(day).getMonth() === anchorMonth;
          const isToday = day === today;
          const dayNum = parseIsoDate(day).getDate();

          return (
            <div
              key={day}
              role="button"
              tabIndex={0}
              onClick={() => onCreateOn(day)}
              onKeyDown={(e) => e.key === 'Enter' && onCreateOn(day)}
              className={cn(
                'min-h-24 cursor-pointer border-b border-r p-1 transition-colors hover:bg-accent/40',
                !inMonth && 'bg-muted/30 text-muted-foreground',
              )}
            >
              <div
                className={cn(
                  'mb-1 flex size-6 items-center justify-center rounded-full text-xs',
                  isToday && 'bg-primary font-semibold text-primary-foreground',
                )}
              >
                {dayNum}
              </div>
              <div className="space-y-0.5">
                {dayItems.slice(0, 3).map((item) => (
                  <EventChip
                    key={`${item.id}-${item.occurrenceDate}`}
                    item={item}
                    onSelect={onSelect}
                    selected={selectedId === item.id}
                    compact
                  />
                ))}
                {dayItems.length > 3 && (
                  <p className="px-1 text-[11px] text-muted-foreground">
                    +{dayItems.length - 3} mais
                  </p>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// The Monday-padded month range. Kept local so MonthView is self-contained.
function monthRange(anchorDate: string): { start: string; end: string } {
  const date = parseIsoDate(anchorDate);
  const firstOfMonth = new Date(date.getFullYear(), date.getMonth(), 1);
  const lastOfMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0);
  const toIso = (d: Date) =>
    `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  const mondayOf = (iso: string) => {
    const d = parseIsoDate(iso);
    const mondayFirst = (d.getDay() + 6) % 7;
    d.setDate(d.getDate() - mondayFirst);
    return toIso(d);
  };
  return { start: mondayOf(toIso(firstOfMonth)), end: addDays(mondayOf(toIso(lastOfMonth)), 6) };
}
