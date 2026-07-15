import { useMemo } from 'react';
import { PageHeader } from '@/shared/components/PageHeader';
import { KpiCards } from '@/modules/dashboard/components/KpiCards';
import { ConsumptionBarChart } from '@/modules/dashboard/components/ConsumptionBarChart';
import {
  useBelowMinimumSummary,
  useConsumptionSeries,
  useExpirySummary,
} from '@/modules/dashboard/api/dashboard.queries';

/** ISO yyyy-MM-dd for `daysAgo` days before today (0 = today). */
function isoDaysAgo(daysAgo: number): string {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  return d.toISOString().slice(0, 10);
}

/**
 * Dashboard "mother" screen: owns the data fetching and composes independent,
 * presentational child components (KpiCards, ConsumptionBarChart). Children stay
 * dumb — they receive data via props. This is the reference pattern every module
 * page follows.
 */
export function DashboardPage() {
  const { from, to } = useMemo(() => ({ from: isoDaysAgo(29), to: isoDaysAgo(0) }), []);

  const expiry = useExpirySummary();
  const belowMinimum = useBelowMinimumSummary();
  const consumption = useConsumptionSeries(from, to);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Painel"
        description="Visão geral do estoque e do consumo dos últimos 30 dias."
      />

      <KpiCards
        expiry={expiry.data}
        belowMinimum={belowMinimum.data}
        loading={expiry.isLoading || belowMinimum.isLoading}
      />

      <ConsumptionBarChart series={consumption.data} loading={consumption.isLoading} />
    </div>
  );
}
