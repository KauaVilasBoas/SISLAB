import { CalendarClock, CalendarX2, CheckCircle2, PackageMinus } from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/components/ui/card';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import type { BelowMinimumSummary, ExpirySummary } from '@/modules/dashboard/types';

interface KpiCardsProps {
  expiry?: ExpirySummary;
  belowMinimum?: BelowMinimumSummary;
  loading?: boolean;
}

interface Kpi {
  label: string;
  value: number | undefined;
  icon: LucideIcon;
  accent: string;
}

/**
 * Presentational KPI row. Receives already-fetched summaries from the mother
 * screen and renders the four headline numbers — no data fetching of its own.
 */
export function KpiCards({ expiry, belowMinimum, loading }: KpiCardsProps) {
  const kpis: Kpi[] = [
    {
      label: 'Vencidos',
      value: expiry?.expired,
      icon: CalendarX2,
      accent: 'text-status-expired',
    },
    {
      label: 'A vencer (30d)',
      value: expiry?.expiringSoon,
      icon: CalendarClock,
      accent: 'text-status-warning',
    },
    {
      label: 'Válidos',
      value: expiry?.ok,
      icon: CheckCircle2,
      accent: 'text-status-ok',
    },
    {
      label: 'Abaixo do mínimo',
      value: belowMinimum?.belowMinimumCount,
      icon: PackageMinus,
      accent: 'text-status-info',
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
          <CardContent>
            <div className="text-2xl font-bold">
              {loading ? (
                <span className="inline-block h-7 w-12 animate-pulse rounded bg-muted" />
              ) : (
                formatNumber(kpi.value)
              )}
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
