import type { PlateConditionResult, PlateWellResult } from '@/modules/experiments/types';

interface PlateConditionsTableProps {
  conditions: PlateConditionResult[];
  /** All result wells, used to list the replicates that were excluded from the aggregates. */
  wells: PlateWellResult[];
  /** Formats the aggregated value with its unit (e.g. "50%" viability, "18.4 µM" nitric oxide). */
  formatComputed: (value: number) => string;
}

/**
 * Per-condition replicate summary (SISLAB-07): one row per compound × concentration with the number of
 * replicates that fed it, the mean and the sample standard deviation, all read from the frozen snapshot.
 *
 * Excluded outlier wells (SISLAB-06) are already left out of every mean by the backend — the snapshot only
 * aggregates the included replicates. To make that explicit, the excluded replicates present on the plate
 * are listed below the table, struck through, so it is clear which wells did NOT enter any average.
 */
export function PlateConditionsTable({
  conditions,
  wells,
  formatComputed,
}: PlateConditionsTableProps) {
  if (conditions.length === 0) {
    return (
      <p className="rounded-md border border-dashed p-6 text-center text-sm text-muted-foreground">
        Nenhuma condição agregada. As médias por condição aparecem após o cálculo.
      </p>
    );
  }

  const excludedWells = wells.filter((well) => well.isExcluded);

  return (
    <div className="space-y-3">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr className="border-b text-left text-xs font-medium uppercase tracking-wide text-muted-foreground">
              <th className="py-2 pr-3">Composto</th>
              <th className="py-2 pr-3">Concentração</th>
              <th className="py-2 pr-3 text-center">Réplicas</th>
              <th className="py-2 pr-3 text-right">Média</th>
              <th className="py-2 pr-3 text-right">Desvio (DP)</th>
              <th className="py-2">Poços</th>
            </tr>
          </thead>
          <tbody>
            {conditions.map((condition, index) => (
              <tr
                key={`${condition.sampleId ?? 'sample'}-${condition.concentrationUm ?? index}`}
                className="border-b last:border-0"
              >
                <td className="py-2 pr-3 font-medium">{condition.sampleId ?? '—'}</td>
                <td className="py-2 pr-3">
                  {condition.concentrationUm != null
                    ? `${condition.concentrationUm} µM`
                    : '—'}
                </td>
                <td className="py-2 pr-3 text-center tabular-nums">
                  {condition.replicateCount}
                </td>
                <td className="py-2 pr-3 text-right tabular-nums">
                  {formatComputed(condition.mean)}
                </td>
                <td className="py-2 pr-3 text-right tabular-nums text-muted-foreground">
                  {condition.standardDeviation != null
                    ? `± ${formatComputed(condition.standardDeviation)}`
                    : '—'}
                </td>
                <td className="py-2">
                  <div className="flex flex-wrap gap-1">
                    {condition.wells.map((coordinate) => (
                      <span
                        key={coordinate}
                        className="inline-flex items-center rounded border px-1.5 py-0.5 text-[11px] font-medium tabular-nums"
                      >
                        {coordinate}
                      </span>
                    ))}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {excludedWells.length > 0 && (
        <div className="rounded-md border border-destructive/30 bg-destructive/5 p-3">
          <p className="text-xs font-medium text-destructive">
            Réplicas excluídas (não entram em nenhuma média nem no desvio):
          </p>
          <div className="mt-1.5 flex flex-wrap gap-1">
            {excludedWells.map((well) => (
              <span
                key={`${well.row}${well.column}`}
                title={well.exclusionReason ?? 'Excluído'}
                className="inline-flex items-center rounded border border-destructive/60 px-1.5 py-0.5 text-[11px] font-medium tabular-nums text-destructive line-through decoration-destructive"
              >
                {well.row}
                {well.column}
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
