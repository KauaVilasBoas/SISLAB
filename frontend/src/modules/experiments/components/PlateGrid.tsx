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
}

/**
 * The 8×12 plate rendered as a grid (cards [E11] #68 / #72). Each designed well is tinted by its role; once
 * the experiment is calculated, sample/standard wells show their computed value (% viability or NO µM,
 * formatted by the caller from the experiment type). Empty (undesigned) positions render as faint placeholders
 * so the plate geometry is always visible.
 */
export function PlateGrid({ wells, isCalculated, formatComputed }: PlateGridProps) {
  const byCoordinate = new Map(wells.map((well) => [`${well.row}${well.column}`, well]));

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
                return (
                  <td key={col}>
                    <div
                      title={`${row}${col} · ${presentation.label}${
                        well.rawAbsorbance != null ? ` · abs ${well.rawAbsorbance}` : ''
                      }`}
                      className={cn(
                        'flex h-12 w-14 flex-col items-center justify-center rounded border text-[10px] leading-tight',
                        presentation.cellClass,
                      )}
                    >
                      <span className="font-semibold">
                        {row}
                        {col}
                      </span>
                      {isCalculated && well.computedValue != null ? (
                        <span>{formatComputed(well.computedValue)}</span>
                      ) : well.rawAbsorbance != null ? (
                        <span>{well.rawAbsorbance}</span>
                      ) : null}
                    </div>
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
      </div>
    </div>
  );
}
