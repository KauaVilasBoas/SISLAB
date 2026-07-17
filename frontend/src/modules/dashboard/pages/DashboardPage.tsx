import { useMemo } from 'react';
import { DashboardHeader } from '@/modules/dashboard/components/DashboardHeader';
import { KpiCards } from '@/modules/dashboard/components/KpiCards';
import { ExpiryDonutChart } from '@/modules/dashboard/components/ExpiryDonutChart';
import { ConsumptionActivityChart } from '@/modules/dashboard/components/ConsumptionActivityChart';
import { ActiveAlerts } from '@/modules/dashboard/components/ActiveAlerts';
import { isoDaysAgo } from '@/modules/dashboard/components/dashboard-presentation';
import {
  useBelowMinimumItems,
  useBelowMinimumSummary,
  useConsumptionSeries,
  useExpiringItems,
  useExpirySummary,
  useOverdueCalibration,
} from '@/modules/dashboard/api/dashboard.queries';

/**
 * Dashboard "mother" screen (card [E7] #49): owns the data fetching and composes independent,
 * presentational child widgets — the greeting header, the KPI row, the validity donut, the active
 * alerts and the self-contained consumption activity chart (which owns its own period tabs). Children
 * stay dumb — they receive data via props. This is the reference pattern every module page follows.
 */
export function DashboardPage() {
  // The KPI's consumption trend uses a fixed 30-day window, independent of the activity chart's tabs.
  const { from, to } = useMemo(() => ({ from: isoDaysAgo(29), to: isoDaysAgo(0) }), []);

  const expiry = useExpirySummary();
  const belowMinimum = useBelowMinimumSummary();
  const consumption = useConsumptionSeries(from, to);
  const expiringItems = useExpiringItems();
  const belowMinimumItems = useBelowMinimumItems();
  const overdueCalibration = useOverdueCalibration();

  // Number of already-expired items that are controlled drugs — the "Itens vencidos" KPI footer.
  const expiredControlledCount = useMemo(
    () =>
      (expiringItems.data?.items ?? []).filter(
        (item) => item.daysRemaining < 0 && item.isControlled,
      ).length,
    [expiringItems.data],
  );

  return (
    <div className="space-y-6">
      <DashboardHeader />

      <KpiCards
        expiry={expiry.data}
        belowMinimum={belowMinimum.data}
        consumption={consumption.data}
        expiredControlledCount={expiredControlledCount}
        loading={expiry.isLoading || belowMinimum.isLoading}
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="lg:col-span-2">
          <ConsumptionActivityChart />
        </div>
        <ExpiryDonutChart summary={expiry.data} loading={expiry.isLoading} />
      </div>

      <ActiveAlerts
        expiring={expiringItems.data?.items}
        belowMinimum={belowMinimumItems.data?.items}
        overdueCalibration={overdueCalibration.data?.items}
        loading={
          expiringItems.isLoading ||
          belowMinimumItems.isLoading ||
          overdueCalibration.isLoading
        }
      />
    </div>
  );
}
