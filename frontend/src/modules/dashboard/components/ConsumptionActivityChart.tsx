import { useMemo, useState } from 'react';
import { ChartCard } from '@/shared/components/ChartCard';
import type { EChartsCoreOption } from '@/shared/lib/echarts';
import { formatDate } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { useConsumptionSeries } from '@/modules/dashboard/api/dashboard.queries';
import {
  ACTIVITY_PERIODS,
  DEFAULT_ACTIVITY_PERIOD,
  STATUS_COLORS,
  windowFor,
  type ActivityPeriodKey,
} from '@/modules/dashboard/components/dashboard-presentation';
import type { ConsumptionSeries } from '@/modules/dashboard/types';

/**
 * Builds a stacked area/line ECharts option from the consumption series: x-axis is the ordered bucket
 * start, one smooth area series per unit (the read side never converts between units, so units are
 * kept apart). See https://echarts.apache.org/examples/en/index.html#chart-type-line
 */
function buildAreaOption(series: ConsumptionSeries): EChartsCoreOption {
  const categories = Array.from(new Set(series.points.map((p) => p.bucketStart))).sort();
  const units = Array.from(new Set(series.points.map((p) => p.unit))).sort();
  const byKey = new Map(
    series.points.map((p) => [`${p.bucketStart}|${p.unit}`, p.totalConsumed]),
  );
  const palette = [STATUS_COLORS.info, STATUS_COLORS.ok, STATUS_COLORS.warning];

  return {
    tooltip: { trigger: 'axis' },
    legend: { bottom: 0 },
    grid: { left: 8, right: 16, top: 16, bottom: 40, containLabel: true },
    color: palette,
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: categories.map((c) => formatDate(c)),
    },
    yAxis: { type: 'value' },
    series: units.map((unit) => ({
      name: unit,
      type: 'line',
      smooth: true,
      showSymbol: false,
      areaStyle: { opacity: 0.15 },
      emphasis: { focus: 'series' },
      data: categories.map((c) => byKey.get(`${c}|${unit}`) ?? 0),
    })),
  };
}

const EMPTY_OPTION: EChartsCoreOption = {};

/** The 7d/30d/3m tabs — plain buttons (no shadcn Tabs primitive in the kit yet). */
function PeriodTabs({
  active,
  onChange,
}: {
  active: ActivityPeriodKey;
  onChange: (key: ActivityPeriodKey) => void;
}) {
  return (
    <div className="inline-flex rounded-md border bg-muted/40 p-0.5">
      {ACTIVITY_PERIODS.map((period) => (
        <button
          key={period.key}
          type="button"
          onClick={() => onChange(period.key)}
          className={cn(
            'rounded px-3 py-1 text-xs font-medium transition-colors',
            active === period.key
              ? 'bg-background text-foreground shadow-sm'
              : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {period.label}
        </button>
      ))}
    </div>
  );
}

/**
 * Self-contained activity widget: owns the selected period (7d/30d/3m) and fetches its own series for
 * that window. This is a deliberate exception to the "mother screen fetches" rule — the tabs are an
 * interactive control local to this chart, so co-locating the period state with its query keeps the
 * dashboard page from re-rendering (and re-fetching) every sibling widget on a tab switch.
 */
export function ConsumptionActivityChart() {
  const [periodKey, setPeriodKey] = useState<ActivityPeriodKey>(DEFAULT_ACTIVITY_PERIOD);

  const period = ACTIVITY_PERIODS.find((p) => p.key === periodKey) ?? ACTIVITY_PERIODS[1];
  const { from, to } = useMemo(() => windowFor(period), [period]);

  const { data, isLoading, isError } = useConsumptionSeries(from, to);

  const isEmpty = !isLoading && !isError && (!data || data.points.length === 0);
  const option = useMemo(
    () => (data && data.points.length > 0 ? buildAreaOption(data) : EMPTY_OPTION),
    [data],
  );

  return (
    <ChartCard
      title="Atividade de consumo"
      description="Total consumido por unidade no período selecionado."
      option={option}
      loading={isLoading}
      isEmpty={isEmpty || isError}
      emptyLabel={
        isError
          ? 'Não foi possível carregar o consumo.'
          : 'Sem consumo registrado no período.'
      }
      height={320}
      actions={<PeriodTabs active={periodKey} onChange={setPeriodKey} />}
    />
  );
}
