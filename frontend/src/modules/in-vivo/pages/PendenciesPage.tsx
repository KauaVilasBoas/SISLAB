import { useNavigate } from 'react-router-dom';
import { Loader2, CheckCircle2 } from 'lucide-react';
import { PageHeader } from '@/shared/components/PageHeader';
import { Badge } from '@/shared/components/ui/badge';
import { usePendencies } from '@/modules/in-vivo/api/pendencies.queries';
import { formatDate, pendencyKindPresentation } from '@/modules/in-vivo/presentation';
import type { PendencyItem, PendencyKind } from '@/modules/in-vivo/types';

/**
 * Pendencies panel (card [E11] #90): the operator's open work across the Experiments module — experiments
 * awaiting calculation, unperformed steps, and biobank samples still awaiting analysis. A read-only dashboard;
 * each row navigates to the item it references so the operator can act on it.
 */
export function PendenciesPage() {
  const navigate = useNavigate();
  const { data, isLoading, isError } = usePendencies();

  const items = data?.items ?? [];

  function routeFor(item: PendencyItem): string {
    return item.kind === 'SampleAwaitingAnalysis'
      ? `/experiments/in-vivo/biobank/${item.referenceId}`
      : `/experiments/${item.referenceId}`;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Pendências"
        description="Trabalho em aberto — cálculos, etapas não realizadas e amostras sem análise."
      />

      {data && (
        <div className="grid gap-4 sm:grid-cols-3">
          <SummaryCard label="Aguardando cálculo" count={data.awaitingCalculationCount} />
          <SummaryCard label="Etapas pendentes" count={data.pendingStepCount} />
          <SummaryCard
            label="Amostras sem análise"
            count={data.sampleAwaitingAnalysisCount}
          />
        </div>
      )}

      <div className="rounded-lg border bg-card">
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando pendências…
          </div>
        ) : isError ? (
          <p className="p-10 text-center text-sm text-destructive">
            Não foi possível carregar as pendências.
          </p>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center gap-2 p-10 text-center text-sm text-muted-foreground">
            <CheckCircle2 className="size-6 text-emerald-500" />
            Nenhuma pendência. Tudo em dia.
          </div>
        ) : (
          <table className="w-full text-sm">
            <thead className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <tr>
                <th className="px-4 py-3 font-medium">Tipo</th>
                <th className="px-4 py-3 font-medium">Item</th>
                <th className="px-4 py-3 font-medium">Detalhe</th>
                <th className="px-4 py-3 font-medium">Desde</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item, index) => {
                const presentation = pendencyKindPresentation[item.kind as PendencyKind];
                return (
                  <tr
                    key={`${item.kind}-${item.referenceId}-${index}`}
                    onClick={() => navigate(routeFor(item))}
                    className="cursor-pointer border-b last:border-0 transition-colors hover:bg-accent/50"
                  >
                    <td className="px-4 py-3">
                      <Badge variant={presentation?.variant ?? 'muted'}>
                        {presentation?.label ?? item.kind}
                      </Badge>
                    </td>
                    <td className="px-4 py-3 font-medium">{item.title}</td>
                    <td className="px-4 py-3 text-muted-foreground">{item.detail}</td>
                    <td className="px-4 py-3 text-muted-foreground">
                      {formatDate(item.sinceUtc)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}

function SummaryCard({ label, count }: { label: string; count: number }) {
  return (
    <div className="rounded-lg border bg-card p-4">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="mt-1 text-2xl font-semibold">{count}</p>
    </div>
  );
}
