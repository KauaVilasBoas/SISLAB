import { Activity, CalendarClock, CalendarX2, PackageMinus, TrendingDown, TrendingUp } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import type {
  BelowMinimumSummary,
  ConsumptionSeries,
  ExpirySummary,
} from '@/modules/dashboard/types';

interface KpiCardsProps {
  expiry?: ExpirySummary;
  belowMinimum?: BelowMinimumSummary;
  consumption?: ConsumptionSeries;
  /** Number of expired items that are controlled drugs (derived from the expiring list). */
  expiredControlledCount?: number;
  loading?: boolean;
}

interface KpiTrend {
  /** Signed percentage; sign drives the arrow and tone. */
  percentage: number | null;
  /** When true, a rising value is bad (e.g. more expired items) → red on up, green on down. */
  higherIsWorse: boolean;
}

interface Kpi {
  label: string;
  value: number | undefined;
  icon: LucideIcon;
  accent: string;
  footer: string;
  trend?: KpiTrend;
}

/** Formats a signed percentage as e.g. "+12%" / "-4%"; null renders as "novo" (no baseline). */
function formatDelta(percentage: number | null): string {
  if (percentage === null) return 'novo';
  const rounded = Math.round(percentage);
  return `${rounded > 0 ? '+' : ''}${rounded}%`;
}

/**
 * Sums the current period total across every unit — the read side keeps consumption per unit (it
 * never converts between units), so the KPI headline is the count of consumption records' units'
 * combined magnitude; the per-unit breakdown lives in the activity chart. The delta is taken from the
 * unit with the largest current total, the most representative trend for a single headline badge.
 */
function summarizeConsumption(series: ConsumptionSeries | undefined): {
  total: number;
  delta: number | null;
} {
  if (!series || series.totals.length === 0) return { total: 0, delta: null };

  const total = series.totals.reduce((sum, t) => sum + t.currentTotal, 0);
  const dominant = series.totals.reduce((a, b) => (b.currentTotal > a.currentTotal ? b : a));
  return { total, delta: dominant.deltaPercentage };
}

function TrendBadge({ trend }: { trend: KpiTrend }) {
  const { percentage, higherIsWorse } = trend;
  const isUp = percentage !== null && percentage > 0;
  const isFlat = percentage === null || percentage === 0;
  const isBad = percentage !== null && (higherIsWorse ? percentage > 0 : percentage < 0);
  const Icon = isUp ? TrendingUp : TrendingDown;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 text-xs font-medium',
        isFlat ? 'text-muted-foreground' : isBad ? 'text-status-expired' : 'text-status-ok',
      )}
    >
      {isFlat ? null : <Icon className="size-3.5" />}
      {formatDelta(percentage)}
    </span>
  );
}

/**
 * Presentational KPI row. Receives already-fetched summaries from the mother screen and renders the
 * four headline numbers with a trend badge and a contextual footer — no data fetching of its own.
 */
export function KpiCards({
  expiry,
  belowMinimum,
  consumption,
  expiredControlledCount,
  loading,
}: KpiCardsProps) {
  const activity = summarizeConsumption(consumption);
  const controlledSuffix =
    expiredControlledCount && expiredControlledCount > 0
      ? `${formatNumber(expiredControlledCount)} controlado(s)`
      : 'nenhum controlado';

  const kpis: Kpi[] = [
    {
      label: 'Itens vencidos',
      value: expiry?.expired,
      icon: CalendarX2,
      accent: 'text-status-expired',
      footer: controlledSuffix,
    },
    {
      label: 'A vencer (≤30d)',
      value: expiry?.expiringSoon,
      icon: CalendarClock,
      accent: 'text-status-warning',
      footer: 'validade próxima',
    },
    {
      label: 'Abaixo do mínimo',
      value: belowMinimum?.belowMinimumCount,
      icon: PackageMinus,
      accent: 'text-status-info',
      footer: 'requer reposição',
    },
    {
      label: 'Consumo (período)',
      value: activity.total,
      icon: Activity,
      accent: 'text-primary',
      footer: 'vs. período anterior',
      trend: { percentage: activity.delta, higherIsWorse: false },
    },
  ];

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {kpis.map((kpi) => (
        <Card key={kpi.label}>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              {kpi.label}
            </CardTitle>
            <kpi.icon className={cn('size-4', kpi.accent)} />
          </CardHeader>
          <CardContent className="space-y-1">
            <div className="flex items-baseline justify-between">
              <span className="text-2xl font-bold">
                {loading ? (
                  <span className="inline-block h-7 w-12 animate-pulse rounded bg-muted" />
                ) : (
                  formatNumber(kpi.value)
                )}
              </span>
              {!loading && kpi.trend ? <TrendBadge trend={kpi.trend} /> : null}
            </div>
            <p className="text-xs text-muted-foreground">{kpi.footer}</p>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
