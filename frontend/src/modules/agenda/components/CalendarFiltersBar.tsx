import { useId } from 'react';
import { X } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { Switch } from '@/shared/components/ui/switch';
import { Tooltip } from '@/shared/components/ui/tooltip';
import { ACTIVITY_TYPE_LABEL } from '@/modules/agenda/presentation';
import { ExperimentSelect } from '@/modules/agenda/components/ExperimentSelect';
import type { AgendaActivityType } from '@/modules/agenda/types';
import type { CalendarFilters } from '@/modules/agenda/api/entries.queries';

/**
 * Filter bar for the calendar (card [E10.7]): filter by activity type, responsible and experiment, plus a
 * "Minha agenda" toggle (onlyMine). Active filters render as removable chips. The parent owns the filter state
 * (persisted in the URL query string) — this component is controlled.
 */
interface CalendarFiltersBarProps {
  filters: CalendarFilters;
  experimentLabel: string | null;
  onChange: (patch: Partial<CalendarFilters>) => void;
  onExperimentChange: (id: string | null, label: string | null) => void;
  onClear: () => void;
}

const ACTIVITY_TYPES: AgendaActivityType[] = [
  'RoomBooking',
  'Experiment',
  'Bioterium',
  'Presentation',
  'Other',
];

export function CalendarFiltersBar({
  filters,
  experimentLabel,
  onChange,
  onExperimentChange,
  onClear,
}: CalendarFiltersBarProps) {
  const onlyMineId = useId();
  const hasActiveFilters =
    !!filters.activityType || !!filters.responsibleId || !!filters.experimentId || !!filters.onlyMine;

  return (
    <div className="space-y-3 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-end gap-3">
        <div className="space-y-1.5">
          <label htmlFor="filter-type" className="text-xs font-medium text-muted-foreground">
            Tipo
          </label>
          <select
            id="filter-type"
            value={filters.activityType ?? ''}
            onChange={(e) => onChange({ activityType: e.target.value || undefined })}
            className="flex h-9 w-44 rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          >
            <option value="">Todos os tipos</option>
            {ACTIVITY_TYPES.map((type) => (
              <option key={type} value={type}>
                {ACTIVITY_TYPE_LABEL[type]}
              </option>
            ))}
          </select>
        </div>

        <div className="w-56">
          <ExperimentSelect
            value={filters.experimentId ?? null}
            valueLabel={experimentLabel}
            onChange={onExperimentChange}
          />
        </div>

        <Tooltip content="Exibe apenas as entradas que você criou ou nas quais você é o responsável.">
          <div className="flex h-9 items-center gap-2">
            <Switch
              id={onlyMineId}
              checked={filters.onlyMine ?? false}
              onCheckedChange={(checked) => onChange({ onlyMine: checked })}
            />
            <label htmlFor={onlyMineId} className="cursor-pointer select-none text-sm font-medium">
              Somente eu
            </label>
          </div>
        </Tooltip>

        {hasActiveFilters && (
          <Button variant="ghost" size="sm" onClick={onClear} className="ml-auto">
            Limpar filtros
          </Button>
        )}
      </div>

      {hasActiveFilters && (
        <div className="flex flex-wrap gap-1.5">
          {filters.activityType && (
            <FilterChip
              label={`Tipo: ${ACTIVITY_TYPE_LABEL[filters.activityType as AgendaActivityType]}`}
              onRemove={() => onChange({ activityType: undefined })}
            />
          )}
          {filters.experimentId && (
            <FilterChip
              label={`Experimento: ${experimentLabel ?? filters.experimentId}`}
              onRemove={() => onExperimentChange(null, null)}
            />
          )}
          {filters.responsibleId && (
            <FilterChip
              label="Responsável filtrado"
              onRemove={() => onChange({ responsibleId: undefined })}
            />
          )}
          {filters.onlyMine && (
            <FilterChip label="Somente eu" onRemove={() => onChange({ onlyMine: false })} />
          )}
        </div>
      )}
    </div>
  );
}

function FilterChip({ label, onRemove }: { label: string; onRemove: () => void }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-muted px-2.5 py-1 text-xs font-medium">
      {label}
      <button
        type="button"
        aria-label={`Remover filtro ${label}`}
        onClick={onRemove}
        className="rounded-full p-0.5 hover:bg-accent"
      >
        <X className="size-3" />
      </button>
    </span>
  );
}
