import { useMemo } from 'react';
import { ChartCard } from '@/shared/components/ChartCard';
import type { EChartsCoreOption } from '@/shared/lib/echarts';
import { formatDate } from '@/shared/lib/format';
import type { ConsumptionSeries } from '@/modules/dashboard/types';

interface ConsumptionBarChartProps {
  series?: ConsumptionSeries;
  loading?: boolean;
}

/**
 * Builds a grouped ECharts bar option from the consumption series: x-axis is the
 * ordered bucket start, one bar series per unit (the read side never converts
 * between units). See https://echarts.apache.org/examples/en/index.html#chart-type-bar
 */
function buildBarOption(series: ConsumptionSeries): EChartsCoreOption {
  const categories = Array.from(new Set(series.points.map((p) => p.bucketStart))).sort();
  const units = Array.from(new Set(series.points.map((p) => p.unit))).sort();

  const byKey = new Map(
    series.points.map((p) => [`${p.bucketStart}|${p.unit}`, p.totalConsumed]),
  );

  return {
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    legend: { bottom: 0 },
    grid: { left: 8, right: 16, top: 24, bottom: 40, containLabel: true },
    xAxis: {
      type: 'category',
      data: categories.map((c) => formatDate(c)),
      axisTick: { alignWithLabel: true },
    },
    yAxis: { type: 'value' },
    series: units.map((unit) => ({
      name: unit,
      type: 'bar',
      emphasis: { focus: 'series' },
      data: categories.map((c) => byKey.get(`${c}|${unit}`) ?? 0),
    })),
  };
}

const EMPTY_OPTION: EChartsCoreOption = {};

export function ConsumptionBarChart({ series, loading }: ConsumptionBarChartProps) {
  const isEmpty = !loading && (!series || series.points.length === 0);
  const option = useMemo(
    () => (series && series.points.length > 0 ? buildBarOption(series) : EMPTY_OPTION),
    [series],
  );

  return (
    <ChartCard
      title="Consumo por período"
      description="Total consumido por unidade, no intervalo selecionado."
      option={option}
      loading={loading}
      isEmpty={isEmpty}
      emptyLabel="Sem consumo registrado no período."
      height={340}
    />
  );
}
