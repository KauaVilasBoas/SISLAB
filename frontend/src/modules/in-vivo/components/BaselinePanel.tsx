import { useState } from 'react';
import { BarChart3, Loader2 } from 'lucide-react';
import { Input } from '@/shared/components/ui/input';
import { Label } from '@/shared/components/ui/label';
import {
  useBaselineByCage,
  useBaselineByGroup,
} from '@/modules/in-vivo/api/projects.queries';
import { formatAmount, formatMeasurement } from '@/modules/in-vivo/presentation';
import type { CageBaselineItem, GroupBaselineItem } from '@/modules/in-vivo/types';

type View = 'cage' | 'group';

/**
 * Baseline summary panel (SISLAB-03): the mean/min/max of one physiological parameter (e.g. glicemia),
 * summarized either by cage (the pre-randomization view — "dados por caixa") or by treatment group (the
 * post-randomization balance check — "reordena o basal por grupo"), as the researcher exports to Prism.
 *
 * The operator types the parameter code (a cadaster value, e.g. "glicemia") and an optional timepoint; the
 * read-side query only fires once a code is present. A thin orchestration shell around the two baseline hooks.
 */
export function BaselinePanel({
  projectId,
  batchId,
}: {
  projectId: string;
  batchId: string;
}) {
  const [view, setView] = useState<View>('cage');
  const [parameterCode, setParameterCode] = useState('glicemia');
  const [timepoint, setTimepoint] = useState('basal');

  const trimmedCode = parameterCode.trim();
  const trimmedTimepoint = timepoint.trim();

  const byCage = useBaselineByCage(projectId, batchId, trimmedCode, trimmedTimepoint);
  const byGroup = useBaselineByGroup(projectId, batchId, trimmedCode, trimmedTimepoint);

  const active = view === 'cage' ? byCage : byGroup;

  return (
    <div className="rounded-lg border bg-card">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b p-4">
        <div className="flex items-center gap-2">
          <BarChart3 className="size-4 text-muted-foreground" />
          <span className="text-sm font-semibold">Basal</span>
          <span className="text-xs text-muted-foreground">
            média / mín / máx por caixa e por grupo
          </span>
        </div>
        <div
          role="tablist"
          aria-label="Visão do basal"
          className="inline-flex rounded-lg border p-0.5"
        >
          <ViewTab
            label="Por caixa"
            active={view === 'cage'}
            onClick={() => setView('cage')}
          />
          <ViewTab
            label="Por grupo"
            active={view === 'group'}
            onClick={() => setView('group')}
          />
        </div>
      </div>

      <div className="grid gap-3 border-b p-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="baseline-parameter">Parâmetro</Label>
          <Input
            id="baseline-parameter"
            value={parameterCode}
            onChange={(e) => setParameterCode(e.target.value)}
            placeholder="Ex.: glicemia"
            maxLength={60}
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="baseline-timepoint">Timepoint — opcional</Label>
          <Input
            id="baseline-timepoint"
            value={timepoint}
            onChange={(e) => setTimepoint(e.target.value)}
            placeholder="Ex.: basal"
            maxLength={60}
          />
        </div>
      </div>

      <div className="p-4">
        {trimmedCode === '' ? (
          <p className="py-6 text-center text-sm text-muted-foreground">
            Informe um parâmetro (ex.: glicemia) para ver o basal.
          </p>
        ) : active.isLoading ? (
          <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando basal…
          </div>
        ) : active.isError ? (
          <p className="py-6 text-center text-sm text-destructive">
            Não foi possível carregar o basal.
          </p>
        ) : view === 'cage' ? (
          <CageBaselineTable rows={byCage.data ?? []} />
        ) : (
          <GroupBaselineTable rows={byGroup.data ?? []} />
        )}
      </div>
    </div>
  );
}

function ViewTab({
  label,
  active,
  onClick,
}: {
  label: string;
  active: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={
        active
          ? 'rounded-md bg-primary px-3 py-1 text-xs font-medium text-primary-foreground'
          : 'rounded-md px-3 py-1 text-xs text-muted-foreground hover:text-foreground'
      }
    >
      {label}
    </button>
  );
}

function CageBaselineTable({ rows }: { rows: CageBaselineItem[] }) {
  if (rows.length === 0) {
    return <EmptyRows label="Nenhuma caixa nesta leva." />;
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <StatHeader firstLabel="Caixa" />
        </thead>
        <tbody className="divide-y">
          {rows.map((row) => (
            <tr key={row.cageId}>
              <td className="py-2 pr-3 font-medium">{row.cageName}</td>
              <td className="py-2 pr-3 text-right tabular-nums">
                {row.animalsWithReading}
              </td>
              <td className="py-2 pr-3 text-right tabular-nums">
                {formatMeasurement(row.meanValue, row.unit)}
              </td>
              <td className="py-2 pr-3 text-right tabular-nums text-muted-foreground">
                {formatMeasurement(row.minValue, row.unit)}
              </td>
              <td className="py-2 text-right tabular-nums text-muted-foreground">
                {formatMeasurement(row.maxValue, row.unit)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function GroupBaselineTable({ rows }: { rows: GroupBaselineItem[] }) {
  if (rows.length === 0) {
    return <EmptyRows label="Nenhum grupo nesta leva." />;
  }
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <StatHeader firstLabel="Grupo" />
        </thead>
        <tbody className="divide-y">
          {rows.map((row) => (
            <tr key={row.groupId}>
              <td className="py-2 pr-3">
                <span className="font-medium">{row.groupName}</span>
                <span className="ml-2 text-xs text-muted-foreground">
                  {formatAmount(row.doseAmount, row.doseUnit)}
                </span>
              </td>
              <td className="py-2 pr-3 text-right tabular-nums">
                {row.animalsWithReading}
              </td>
              <td className="py-2 pr-3 text-right tabular-nums">
                {formatMeasurement(row.meanValue, row.unit)}
              </td>
              <td className="py-2 pr-3 text-right tabular-nums text-muted-foreground">
                {formatMeasurement(row.minValue, row.unit)}
              </td>
              <td className="py-2 text-right tabular-nums text-muted-foreground">
                {formatMeasurement(row.maxValue, row.unit)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function StatHeader({ firstLabel }: { firstLabel: string }) {
  return (
    <tr className="text-xs text-muted-foreground">
      <th className="pb-2 pr-3 text-left font-medium">{firstLabel}</th>
      <th className="pb-2 pr-3 text-right font-medium">n</th>
      <th className="pb-2 pr-3 text-right font-medium">Média</th>
      <th className="pb-2 pr-3 text-right font-medium">Mín</th>
      <th className="pb-2 text-right font-medium">Máx</th>
    </tr>
  );
}

function EmptyRows({ label }: { label: string }) {
  return <p className="py-6 text-center text-sm text-muted-foreground">{label}</p>;
}
