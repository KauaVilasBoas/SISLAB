import { cn } from '@/shared/lib/utils';
import { wellRolePresentation } from '@/modules/experiments/components/experiment-presentation';
import type { PlateWellResult } from '@/modules/experiments/types';

const ROWS = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
const COLUMNS = Array.from({ length: 12 }, (_, i) => i + 1);

interface PlateGridProps {
  wells: PlateWellResult[];
  /** Whether the experiment has been calculated (shows the computed value inside the wells). */
  isCalculated: boolean;
  /** Formats a well's computed value for display (e.g. "50%" for viability, "18.4 µM" for nitric oxide). */
  formatComputed: (value: number) => string;
  /**
   * When true, designed wells become clickable to open the outlier-exclusion flow (SISLAB-06). The caller
   * gates this on the exclude permission AND on the experiment not being calculated (the snapshot is frozen
   * once calculated, so exclusion is disabled). Omitted ⇒ the grid is read-only.
   */
  onWellClick?: (well: PlateWellResult) => void;
}

/**
 * The 8×12 plate rendered as a grid (cards [E11] #68 / #72). Each designed well is tinted by its role; once
 * the experiment is calculated, sample/standard wells show their computed value (% viability or NO µM,
 * formatted by the caller from the experiment type). Empty (undesigned) positions render as faint placeholders
 * so the plate geometry is always visible.
 *
 * Outlier exclusion (SISLAB-06): an excluded well is hatched, its value struck through and marked with an "×"
 * badge; its tooltip carries the reason and author. When {@link PlateGridProps.onWellClick} is provided the
 * designed wells are clickable to toggle the exclusion (before calculation only).
 */
export function PlateGrid({ wells, isCalculated, formatComputed, onWellClick }: PlateGridProps) {
  const byCoordinate = new Map(wells.map((well) => [`${well.row}${well.column}`, well]));
  const interactive = Boolean(onWellClick);

  return (
    <div className="overflow-x-auto">
      <table className="border-separate border-spacing-1">
        <thead>
          <tr>
            <th className="w-6" aria-hidden />
            {COLUMNS.map((col) => (
              <th key={col} className="w-14 text-center text-xs font-medium text-muted-foreground">
                {col}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {ROWS.map((row) => (
            <tr key={row}>
              <th className="pr-1 text-right text-xs font-medium text-muted-foreground">{row}</th>
              {COLUMNS.map((col) => {
                const well = byCoordinate.get(`${row}${col}`);
                if (!well) {
                  return (
                    <td key={col}>
                      <div className="flex h-12 w-14 items-center justify-center rounded border border-dashed border-muted text-[10px] text-muted-foreground/40">
                        {row}
                        {col}
                      </div>
                    </td>
                  );
                }
                const presentation = wellRolePresentation[well.role];
                const title = buildTitle(row, col, presentation.label, well);
                const cell = (
                  <div
                    title={title}
                    className={cn(
                      'relative flex h-12 w-14 flex-col items-center justify-center rounded border text-[10px] leading-tight',
                      presentation.cellClass,
                      well.isExcluded && 'opacity-70 ring-1 ring-destructive/60 excluded-hatch',
                      interactive &&
                        'cursor-pointer transition-shadow hover:ring-2 hover:ring-ring focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                    )}
                  >
                    {well.isExcluded && (
                      <span
                        aria-hidden
                        className="absolute -right-1 -top-1 flex size-3.5 items-center justify-center rounded-full bg-destructive text-[8px] font-bold text-destructive-foreground shadow"
                      >
                        ×
                      </span>
                    )}
                    <span className="font-semibold">
                      {row}
                      {col}
                    </span>
                    {renderValue(well, isCalculated, formatComputed)}
                  </div>
                );

                return (
                  <td key={col}>
                    {interactive ? (
                      <button
                        type="button"
                        onClick={() => onWellClick?.(well)}
                        aria-label={`Poço ${row}${col}${well.isExcluded ? ' (excluído)' : ''}`}
                        className="block"
                      >
                        {cell}
                      </button>
                    ) : (
                      cell
                    )}
                  </td>
                );
              })}
            </tr>
          ))}
        </tbody>
      </table>

      <div className="mt-3 flex flex-wrap gap-3 text-xs text-muted-foreground">
        {(Object.keys(wellRolePresentation) as (keyof typeof wellRolePresentation)[]).map((role) => (
          <span key={role} className="inline-flex items-center gap-1.5">
            <span className={cn('inline-block size-3 rounded border', wellRolePresentation[role].cellClass)} />
            {wellRolePresentation[role].label}
          </span>
        ))}
        <span className="inline-flex items-center gap-1.5">
          <span className="excluded-hatch inline-block size-3 rounded border border-destructive/60" />
          Excluído (outlier)
        </span>
      </div>
    </div>
  );
}

/** The value line inside a well: struck through when excluded, computed value once calculated, else absorbance. */
function renderValue(
  well: PlateWellResult,
  isCalculated: boolean,
  formatComputed: (value: number) => string,
) {
  const strike = well.isExcluded ? 'line-through decoration-destructive' : '';
  if (isCalculated && well.computedValue != null) {
    return <span className={strike}>{formatComputed(well.computedValue)}</span>;
  }
  if (well.rawAbsorbance != null) {
    return <span className={strike}>{well.rawAbsorbance}</span>;
  }
  return null;
}

/** Builds the hover tooltip: coordinate · role · absorbance · (exclusion reason/author when excluded). */
function buildTitle(
  row: string,
  col: number,
  roleLabel: string,
  well: PlateWellResult,
): string {
  const parts = [`${row}${col}`, roleLabel];
  if (well.rawAbsorbance != null) parts.push(`abs ${well.rawAbsorbance}`);
  if (well.isExcluded) {
    parts.push(`Excluído: ${well.exclusionReason ?? '—'}`);
    if (well.excludedBy) parts.push(`por ${well.excludedBy}`);
  }
  return parts.join(' · ');
}
