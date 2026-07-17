import { useMemo } from 'react';
import { PageHeader } from '@/shared/components/PageHeader';
import { ChartCard } from '@/shared/components/ChartCard';
import { NoPermissionState } from '@/shared/components/NoPermissionState';
import type { EChartsCoreOption } from '@/shared/lib/echarts';
import { useHasPermission } from '@/modules/auth/PermissionsProvider';
import { Permissions } from '@/modules/auth/permissions';
import {
  useCostByExperiment,
  useCostByMonth,
} from '@/modules/inventory/api/inventory.queries';
import {
  buildCostByExperimentOption,
  buildCostByMonthOption,
} from '@/modules/inventory/components/cost-report-presentation';

const EMPTY_OPTION: EChartsCoreOption = {};
const MONTHS_WINDOW = 12;
const TOP_EXPERIMENTS = 10;
const EMPTY_LABEL = 'Nenhum consumo com custo rastreado ainda.';

/**
 * Cost report screen (card [E4] #109): two ECharts widgets over the active company's priced consumptions —
 * spend per calendar month (last 12) and spend per experiment (top 10). Cost is gestão-sensitive, so the
 * backend endpoints are gated by Inventory.Cost.Read; this page mirrors that gate two ways:
 *  - it renders the "acesso restrito" panel (never a broken chart) when the user lacks the capability, and
 *  - the queries are `enabled` only when the user holds it, so we never fire a guaranteed 403.
 *
 * The route is also wrapped in <RequirePermissionRoute> for direct-URL access; this in-page check keeps the
 * denied state coherent even if reached some other way and skips the doomed fetch. Follows the dashboard
 * "mother screen fetches, presentation stays pure" pattern (the option building lives in the presentation
 * helper).
 */
export function CostReportPage() {
  const canReadCost = useHasPermission(Permissions.inventory.costRead);

  const byMonth = useCostByMonth(MONTHS_WINDOW, canReadCost);
  const byExperiment = useCostByExperiment(TOP_EXPERIMENTS, canReadCost);

  const monthOption = useMemo(
    () =>
      byMonth.data && byMonth.data.length > 0
        ? buildCostByMonthOption(byMonth.data)
        : EMPTY_OPTION,
    [byMonth.data],
  );

  const experimentOption = useMemo(
    () =>
      byExperiment.data && byExperiment.data.length > 0
        ? buildCostByExperimentOption(byExperiment.data)
        : EMPTY_OPTION,
    [byExperiment.data],
  );

  if (!canReadCost) {
    return (
      <NoPermissionState
        title="Relatório de custos"
        description="Você não tem permissão para ver os custos de consumo nesta empresa. Fale com um coordenador se precisar de acesso."
      />
    );
  }

  const monthIsEmpty =
    !byMonth.isLoading && (byMonth.isError || !byMonth.data || byMonth.data.length === 0);
  const experimentIsEmpty =
    !byExperiment.isLoading &&
    (byExperiment.isError || !byExperiment.data || byExperiment.data.length === 0);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Relatório de custos"
        description="Custo dos consumos com preço rastreado, por mês e por experimento."
      />

      <div className="grid gap-6 lg:grid-cols-2">
        <ChartCard
          title="Custo por mês"
          description="Total gasto em consumo por mês (últimos 12 meses)."
          option={monthOption}
          loading={byMonth.isLoading}
          isEmpty={monthIsEmpty}
          emptyLabel={
            byMonth.isError ? 'Não foi possível carregar o custo por mês.' : EMPTY_LABEL
          }
          height={320}
        />

        <ChartCard
          title="Custo por experimento"
          description="Experimentos com maior gasto em consumo (top 10)."
          option={experimentOption}
          loading={byExperiment.isLoading}
          isEmpty={experimentIsEmpty}
          emptyLabel={
            byExperiment.isError
              ? 'Não foi possível carregar o custo por experimento.'
              : EMPTY_LABEL
          }
          height={320}
        />
      </div>
    </div>
  );
}
