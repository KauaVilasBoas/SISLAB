import { Link } from 'react-router-dom';
import {
  Calculator,
  ChevronRight,
  ClipboardList,
  FlaskRound,
  Loader2,
} from 'lucide-react';
import { Badge } from '@/shared/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { formatRelativeTime } from '@/shared/lib/format';
import { usePendencies } from '@/modules/in-vivo/api/pendencies.queries';
import type { PendencyItem, PendencyKind } from '@/modules/in-vivo/types';

const KIND_META: Record<
  PendencyKind,
  { icon: React.ElementType; label: string; to: (id: string) => string }
> = {
  AwaitingCalculation: {
    icon: Calculator,
    label: 'Aguardando cálculo',
    to: (id) => `/experiments/${id}`,
  },
  PendingStep: {
    icon: ClipboardList,
    label: 'Step pendente',
    to: (id) => `/experiments/${id}`,
  },
  SampleAwaitingAnalysis: {
    icon: FlaskRound,
    label: 'Amostra pendente',
    to: (id) => `/experiments/in-vivo/biobank/${id}`,
  },
};

const MAX_ITEMS = 6;

function PendencyRow({ item }: { item: PendencyItem }) {
  const meta = KIND_META[item.kind];
  const Icon = meta.icon;
  return (
    <li>
      <Link
        to={meta.to(item.referenceId)}
        className="flex items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent"
      >
        <Icon className="size-5 shrink-0 text-muted-foreground" />
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm font-medium">{item.title}</p>
          <p className="truncate text-xs text-muted-foreground">
            {item.detail} · {formatRelativeTime(item.sinceUtc)}
          </p>
        </div>
        <ChevronRight className="size-4 shrink-0 text-muted-foreground" />
      </Link>
    </li>
  );
}

/**
 * Dashboard widget for experiment pendencies (card [E7] #91): shows the top open items across the
 * Experiments module — awaiting calculation, pending steps and samples with no completed analysis.
 * Data comes from the same `/api/experiments/pendencies` endpoint as the full pendencies panel.
 */
export function ExperimentPendenciesWidget() {
  const { data, isLoading } = usePendencies();

  const total =
    (data?.awaitingCalculationCount ?? 0) +
    (data?.pendingStepCount ?? 0) +
    (data?.sampleAwaitingAnalysisCount ?? 0);

  const items = data?.items.slice(0, MAX_ITEMS) ?? [];

  return (
    <Card>
      <CardHeader className="flex flex-row items-center gap-2 space-y-0">
        <ClipboardList className="size-4 text-muted-foreground" />
        <CardTitle className="flex-1">Pendências de experimento</CardTitle>
        {!isLoading && total > 0 && (
          <Badge variant="secondary">{total}</Badge>
        )}
      </CardHeader>
      <CardContent>
        {isLoading ? (
          <div className="flex items-center justify-center gap-2 py-6 text-sm text-muted-foreground">
            <Loader2 className="size-4 animate-spin" />
            Carregando…
          </div>
        ) : items.length === 0 ? (
          <p className="py-6 text-center text-sm text-muted-foreground">
            Nenhuma pendência de experimento. Tudo em dia.
          </p>
        ) : (
          <>
            <ul className="space-y-2">
              {items.map((item) => (
                <PendencyRow key={`${item.kind}-${item.referenceId}`} item={item} />
              ))}
            </ul>
            {total > MAX_ITEMS && (
              <div className="mt-3 text-center">
                <Link
                  to="/experiments/in-vivo/pendencies"
                  className="text-sm text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
                >
                  Ver todas ({total})
                </Link>
              </div>
            )}
          </>
        )}
        {!isLoading && items.length > 0 && total <= MAX_ITEMS && (
          <div className="mt-3 text-right">
            <Link
              to="/experiments/in-vivo/pendencies"
              className="text-sm text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
            >
              Ver painel completo →
            </Link>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
