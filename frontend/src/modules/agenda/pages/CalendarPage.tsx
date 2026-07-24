import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { CalendarPlus, ChevronLeft, ChevronRight, Loader2, Rss } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Button } from '@/shared/components/ui/button';
import { useToast } from '@/shared/components/ui/toast';
import { cn } from '@/shared/lib/utils';
import {
  ACTIVITY_TYPE_COLOR,
  ACTIVITY_TYPE_LABEL,
} from '@/modules/agenda/presentation';
import {
  type CalendarView,
  addDays,
  addMonths,
  rangeForView,
  todayIso,
  viewTitle,
} from '@/modules/agenda/lib/calendar';
import {
  useCalendarEntries,
  useCancelOccurrence,
  useDeleteEntry,
  useSubscribeIcal,
  type CalendarFilters,
} from '@/modules/agenda/api/entries.queries';
import { DayView, MonthView, WeekView } from '@/modules/agenda/components/CalendarViews';
import { RoomOccupancyGantt } from '@/modules/agenda/components/RoomOccupancyGantt';
import { EventDetailPanel } from '@/modules/agenda/components/EventDetailPanel';
import { EntryFormModal } from '@/modules/agenda/components/EntryFormModal';
import { CalendarFiltersBar } from '@/modules/agenda/components/CalendarFiltersBar';
import type { AgendaActivityType, CalendarItem } from '@/modules/agenda/types';

const VIEWS: { key: CalendarView; label: string }[] = [
  { key: 'day', label: 'Dia' },
  { key: 'week', label: 'Semana' },
  { key: 'month', label: 'Mês' },
  { key: 'rooms', label: 'Salas' },
];

/**
 * Improved calendar page (cards [E10.5/6/7]) over the unified AgendaEntry API. Owns the view (day/week/month),
 * the anchor date, the filter set and the selected occurrence. Filters + view + date live in the URL query
 * string so a filtered calendar is shareable and survives a refresh.
 */
export function CalendarPage() {
  const toast = useToast();
  const [searchParams, setSearchParams] = useSearchParams();

  const view = (searchParams.get('view') as CalendarView) || 'month';
  const anchorDate = searchParams.get('date') || todayIso();

  const filters: CalendarFilters = useMemo(
    () => ({
      activityType: searchParams.get('activityType') || undefined,
      responsibleId: searchParams.get('responsibleId') || undefined,
      experimentId: searchParams.get('experimentId') || undefined,
      onlyMine: searchParams.get('onlyMine') === 'true' || undefined,
    }),
    [searchParams],
  );
  const [experimentLabel, setExperimentLabel] = useState<string | null>(null);

  const isRoomsView = view === 'rooms';
  const range = rangeForView(view, anchorDate);
  const { data: items = [], isLoading } = useCalendarEntries({
    ...range,
    ...filters,
    // The Salas view has its own occupancy query; suppress the calendar-entries fetch.
    start: isRoomsView ? '' : range.start,
    end: isRoomsView ? '' : range.end,
  });

  const deleteEntry = useDeleteEntry();
  const cancelOccurrence = useCancelOccurrence();
  const subscribeIcal = useSubscribeIcal();

  const [selected, setSelected] = useState<CalendarItem | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editing, setEditing] = useState<CalendarItem | null>(null);
  const [formDefaultDate, setFormDefaultDate] = useState(anchorDate);

  // ---- URL param mutation --------------------------------------------------

  function patchParams(patch: Record<string, string | undefined>) {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        for (const [key, value] of Object.entries(patch)) {
          if (value === undefined || value === '') next.delete(key);
          else next.set(key, value);
        }
        return next;
      },
      { replace: true },
    );
  }

  function setView(next: CalendarView) {
    patchParams({ view: next });
  }

  function navigate(direction: -1 | 1) {
    const nextDate =
      view === 'month' ? addMonths(anchorDate, direction) : addDays(anchorDate, direction * stepDays(view));
    patchParams({ date: nextDate });
  }

  function updateFilters(patch: Partial<CalendarFilters>) {
    patchParams({
      activityType: 'activityType' in patch ? patch.activityType : filters.activityType,
      responsibleId: 'responsibleId' in patch ? patch.responsibleId : filters.responsibleId,
      experimentId: 'experimentId' in patch ? patch.experimentId : filters.experimentId,
      onlyMine:
        'onlyMine' in patch
          ? patch.onlyMine
            ? 'true'
            : undefined
          : filters.onlyMine
            ? 'true'
            : undefined,
    });
  }

  function clearFilters() {
    setExperimentLabel(null);
    patchParams({
      activityType: undefined,
      responsibleId: undefined,
      experimentId: undefined,
      onlyMine: undefined,
    });
  }

  // ---- CRUD handlers -------------------------------------------------------

  function openCreate(isoDate: string) {
    setEditing(null);
    setFormDefaultDate(isoDate);
    setSelected(null);
    setFormOpen(true);
  }

  function openEdit(item: CalendarItem) {
    setEditing(item);
    setFormDefaultDate(item.occurrenceDate);
    setFormOpen(true);
  }

  function handleDelete(item: CalendarItem) {
    const onSuccess = () => {
      toast('success', item.isRecurring ? 'Ocorrência cancelada.' : 'Evento excluído.');
      setSelected(null);
    };
    const onError = () => toast('error', 'Não foi possível excluir.');

    if (item.isRecurring) {
      cancelOccurrence.mutate({ id: item.id, date: item.occurrenceDate }, { onSuccess, onError });
    } else {
      deleteEntry.mutate(item.id, { onSuccess, onError });
    }
  }

  function handleSubscribe() {
    subscribeIcal.mutate(undefined, {
      onSuccess: (result) => {
        const url = `${window.location.origin}/api/agenda/calendar.ics?token=${result.token}`;
        void navigator.clipboard?.writeText(url);
        toast('success', 'Link do feed iCal copiado para a área de transferência.');
      },
      onError: () => toast('error', 'Não foi possível gerar o feed iCal.'),
    });
  }

  const ViewComponent = { day: DayView, week: WeekView, month: MonthView, rooms: DayView }[view];

  return (
    <div className="space-y-5">
      <PageHeader
        title="Calendário"
        description="Eventos, reservas e experimentos em uma agenda unificada."
        actions={
          <div className="flex gap-2">
            <Button variant="outline" onClick={handleSubscribe} disabled={subscribeIcal.isPending}>
              <Rss className="size-4" /> Feed iCal
            </Button>
            <Button onClick={() => openCreate(anchorDate)}>
              <CalendarPlus className="size-4" /> Novo evento
            </Button>
          </div>
        }
      />

      {/* Toolbar: navigation + view toggle */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-1">
          <Button variant="outline" size="icon" aria-label="Anterior" onClick={() => navigate(-1)}>
            <ChevronLeft className="size-4" />
          </Button>
          <Button variant="ghost" size="sm" onClick={() => patchParams({ date: todayIso() })}>
            Hoje
          </Button>
          <Button variant="outline" size="icon" aria-label="Próximo" onClick={() => navigate(1)}>
            <ChevronRight className="size-4" />
          </Button>
        </div>

        <span className="text-sm font-medium capitalize">{viewTitle(view, anchorDate)}</span>

        <div role="tablist" className="ml-auto inline-flex gap-1 rounded-lg bg-muted p-1">
          {VIEWS.map(({ key, label }) => (
            <button
              key={key}
              role="tab"
              type="button"
              aria-selected={view === key}
              onClick={() => setView(key)}
              className={cn(
                'rounded-md px-3 py-1 text-sm font-medium transition-colors',
                view === key
                  ? 'bg-card text-foreground shadow-sm'
                  : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {label}
            </button>
          ))}
        </div>
      </div>

      <CalendarFiltersBar
        filters={filters}
        experimentLabel={experimentLabel}
        onChange={updateFilters}
        onExperimentChange={(id, label) => {
          setExperimentLabel(label);
          updateFilters({ experimentId: id ?? undefined });
        }}
        onClear={clearFilters}
      />

      {/* Legend */}
      <div className="flex flex-wrap gap-3 text-xs text-muted-foreground">
        {(Object.keys(ACTIVITY_TYPE_LABEL) as AgendaActivityType[]).map((type) => (
          <span key={type} className="inline-flex items-center gap-1.5">
            <span className={cn('size-2.5 rounded-full', ACTIVITY_TYPE_COLOR[type].dot)} />
            {ACTIVITY_TYPE_LABEL[type]}
          </span>
        ))}
      </div>

      {/* Calendar + detail panel */}
      <div className="flex gap-4">
        <div className="min-w-0 flex-1">
          {isRoomsView ? (
            <RoomOccupancyGantt date={anchorDate} />
          ) : isLoading ? (
            <div className="flex items-center justify-center gap-2 py-24 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" /> Carregando calendário…
            </div>
          ) : (
            <ViewComponent
              items={items}
              anchorDate={anchorDate}
              onSelect={setSelected}
              selectedId={selected?.id}
              onCreateOn={openCreate}
            />
          )}
        </div>

        {!isRoomsView && selected && (
          <EventDetailPanel
            item={selected}
            onClose={() => setSelected(null)}
            onEdit={() => openEdit(selected)}
            onDelete={() => handleDelete(selected)}
          />
        )}
      </div>

      {formOpen && (
        <EntryFormModal
          open={formOpen}
          onClose={() => setFormOpen(false)}
          editing={editing}
          defaultDate={formDefaultDate}
        />
      )}
    </div>
  );
}

function stepDays(view: CalendarView): number {
  return view === 'week' ? 7 : 1;
}
