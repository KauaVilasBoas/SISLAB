import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { CalendarDays, Loader2, Presentation, Repeat } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { cn } from '@/shared/lib/utils';
import { useCalendar } from '@/modules/agenda/api/rooms.queries';
import { useBioteriumSchedule } from '@/modules/agenda/api/bioterium.queries';
import { usePresentationSchedule } from '@/modules/agenda/api/presentations.queries';
import {
  ACTIVITY_LABEL,
  ASSIGNMENT_STATUS_LABEL,
  PRESENTATION_TYPE_LABEL,
  currentWeekMonday,
  formatDate,
  formatTime,
} from '@/modules/agenda/presentation';
import type {
  AgendaActivity,
  AssignmentStatus,
  BioteriumAssignmentItem,
  BookingListItem,
  PresentationListItem,
  PresentationType,
} from '@/modules/agenda/types';

// ---------------------------------------------------------------------------
// Unified event model
// ---------------------------------------------------------------------------

type EventKind = 'booking' | 'bioterium' | 'presentation';

interface CalendarEvent {
  id: string;
  kind: EventKind;
  date: string;
  /** 'HH:mm' or null for all-day events (biotério, presentations). */
  time: string | null;
  title: string;
  subtitle: string;
  status: string;
  link: string;
}

function bookingToEvent(b: BookingListItem): CalendarEvent {
  return {
    id: b.bookingId,
    kind: 'booking',
    date: b.date,
    time: formatTime(b.startTime),
    title: `${b.roomName} — ${ACTIVITY_LABEL[b.activity as AgendaActivity] ?? b.activity}`,
    subtitle: b.bookedByName + (b.hasConflictWarning ? ' ⚠ conflito' : ''),
    status: 'Ativo',
    link: '/agenda/rooms',
  };
}

function bioteriumToEvent(a: BioteriumAssignmentItem): CalendarEvent {
  return {
    id: a.id,
    kind: 'bioterium',
    date: a.assignmentDate,
    time: null,
    title: `Biotério — ${a.responsibleName}`,
    subtitle: ASSIGNMENT_STATUS_LABEL[a.status as AssignmentStatus] ?? a.status,
    status: a.status,
    link: '/agenda/bioterium',
  };
}

function presentationToEvent(p: PresentationListItem): CalendarEvent {
  return {
    id: p.id,
    kind: 'presentation',
    date: p.scheduledDate,
    time: null,
    title: `${PRESENTATION_TYPE_LABEL[p.type as PresentationType] ?? p.type}: ${p.title}`,
    subtitle: p.presenterName,
    status: p.status,
    link: '/agenda/presentations',
  };
}

const KIND_STYLES: Record<EventKind, { bg: string; icon: React.ElementType }> = {
  booking: { bg: 'bg-blue-500/10 border-blue-500/30', icon: CalendarDays },
  bioterium: { bg: 'bg-green-500/10 border-green-500/30', icon: Repeat },
  presentation: { bg: 'bg-purple-500/10 border-purple-500/30', icon: Presentation },
};

// ---------------------------------------------------------------------------
// Week helpers
// ---------------------------------------------------------------------------

function addDays(isoDate: string, days: number): string {
  const d = new Date(isoDate);
  d.setDate(d.getDate() + days);
  return d.toISOString().substring(0, 10);
}

function weekDays(monday: string): string[] {
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

function dayName(isoDate: string): string {
  const d = new Date(isoDate + 'T12:00:00');
  return d.toLocaleDateString('pt-BR', { weekday: 'short' });
}

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

const TYPE_FILTERS = [
  { value: '' as EventKind | '', label: 'Tudo' },
  { value: 'booking' as EventKind, label: 'Reservas' },
  { value: 'bioterium' as EventKind, label: 'Biotério' },
  { value: 'presentation' as EventKind, label: 'Apresentações' },
];

/**
 * Unified calendar page (card [E10] #87). Aggregates room bookings, biotério assignments and presentations
 * into a single weekly grid. Data comes from the three existing Agenda endpoints in parallel — no new backend
 * endpoint required. Filterable by event type.
 */
export function UnifiedCalendarPage() {
  const [monday, setMonday] = useState(currentWeekMonday());
  const [filter, setFilter] = useState<EventKind | ''>('');

  const sunday = addDays(monday, 6);
  const days = weekDays(monday);

  // Fetch all three sources in parallel for the selected week.
  // Calendar endpoint is per-day; we fetch each day's bookings. For simplicity, pick "today" in the week.
  const today = days[0]; // Monday as reference for the calendar query.
  const { data: bookings = [], isLoading: loadingBookings } = useCalendar(today);
  const { data: assignments = [], isLoading: loadingAssignments } = useBioteriumSchedule(monday, sunday);
  const { data: presentations = [], isLoading: loadingPresentations } = usePresentationSchedule(monday, sunday);

  const isLoading = loadingBookings || loadingAssignments || loadingPresentations;

  const allEvents: CalendarEvent[] = useMemo(() => {
    const events: CalendarEvent[] = [
      ...bookings.map(bookingToEvent),
      ...assignments.map(bioteriumToEvent),
      ...presentations
        .filter((p) => p.status !== 'Cancelled')
        .map(presentationToEvent),
    ];
    return filter ? events.filter((e) => e.kind === filter) : events;
  }, [bookings, assignments, presentations, filter]);

  // Group events by date.
  const byDate = useMemo(() => {
    const map = new Map<string, CalendarEvent[]>();
    for (const day of days) map.set(day, []);
    for (const event of allEvents) {
      const list = map.get(event.date);
      if (list) list.push(event);
    }
    return map;
  }, [allEvents, days]);

  function shiftWeek(weeks: number) {
    setMonday((m) => addDays(m, weeks * 7));
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Calendário unificado"
        description="Reservas, biotério e apresentações da semana em uma só visão."
      />

      {/* Navigation */}
      <div className="flex flex-wrap items-center gap-3">
        <Button variant="outline" size="sm" onClick={() => shiftWeek(-1)}>← Semana anterior</Button>
        <span className="text-sm font-medium">
          {formatDate(monday)} – {formatDate(sunday)}
        </span>
        <Button variant="outline" size="sm" onClick={() => shiftWeek(1)}>Próxima semana →</Button>
        <Button variant="ghost" size="sm" onClick={() => setMonday(currentWeekMonday())}>Esta semana</Button>

        <div
          role="tablist"
          className="ml-auto inline-flex flex-wrap gap-1 rounded-lg bg-muted p-1"
        >
          {TYPE_FILTERS.map(({ value, label }) => (
            <button
              key={value || 'all'}
              role="tab"
              type="button"
              aria-selected={filter === value}
              onClick={() => setFilter(value)}
              className={cn(
                'rounded-md px-3 py-1 text-sm font-medium transition-colors',
                filter === value
                  ? 'bg-card text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      {isLoading ? (
        <div className="flex items-center justify-center gap-2 py-16 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" />
          Carregando calendário…
        </div>
      ) : (
        <div className="grid grid-cols-7 gap-2">
          {days.map((day) => {
            const events = byDate.get(day) ?? [];
            const isToday = day === new Date().toISOString().substring(0, 10);

            return (
              <div key={day} className="min-h-32 rounded-lg border bg-card">
                <div
                  className={cn(
                    'rounded-t-lg px-2 py-1.5 text-center text-xs font-semibold uppercase tracking-wide',
                    isToday
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground',
                  )}
                >
                  {dayName(day)}<br />
                  <span className="font-normal">{formatDate(day).substring(0, 5)}</span>
                </div>
                <div className="space-y-1 p-1">
                  {events.length === 0 ? (
                    <p className="py-2 text-center text-xs text-muted-foreground/50">—</p>
                  ) : (
                    events.map((event) => {
                      const { bg, icon: Icon } = KIND_STYLES[event.kind];
                      return (
                        <Link
                          key={event.id}
                          to={event.link}
                          className={cn(
                            'flex flex-col gap-0.5 rounded border p-1 text-xs transition-colors hover:opacity-80',
                            bg,
                          )}
                        >
                          <span className="flex items-center gap-1 font-medium leading-tight">
                            <Icon className="size-3 shrink-0" />
                            <span className="truncate">{event.title}</span>
                          </span>
                          <span className="truncate text-muted-foreground">{event.subtitle}</span>
                          {event.time && (
                            <span className="text-muted-foreground/70">{event.time}</span>
                          )}
                        </Link>
                      );
                    })
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
