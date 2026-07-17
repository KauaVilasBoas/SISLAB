import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { ChartCard } from '@/shared/components/ChartCard';
import type { EChartsCoreOption } from '@/shared/lib/echarts';
import { formatNumber } from '@/shared/lib/format';
import { STATUS_COLORS } from '@/modules/dashboard/components/dashboard-presentation';
import type { ExpirySummary } from '@/modules/dashboard/types';

interface ExpiryDonutChartProps {
  summary?: ExpirySummary;
  loading?: boolean;
}

interface Slice {
  name: string;
  value: number;
  color: string;
}

function slicesOf(summary: ExpirySummary): Slice[] {
  return [
    { name: 'Vencidos', value: summary.expired, color: STATUS_COLORS.expired },
    { name: 'Vencem ≤30d', value: summary.expiringSoon, color: STATUS_COLORS.warning },
    { name: 'Em dia', value: summary.ok, color: STATUS_COLORS.ok },
  ];
}

/**
 * Builds the "Situação de validade" doughnut option from the three expiry totals. Colors mirror the
 * status palette (red/amber/green). The center shows the total number of items carrying a validity.
 * See https://echarts.apache.org/examples/en/index.html#chart-type-pie
 */
function buildDonutOption(summary: ExpirySummary): EChartsCoreOption {
  const slices = slicesOf(summary);

  return {
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    color: slices.map((s) => s.color),
    series: [
      {
        name: 'Situação de validade',
        type: 'pie',
        radius: ['58%', '80%'],
        avoidLabelOverlap: false,
        itemStyle: { borderRadius: 4, borderColor: 'hsl(var(--card))', borderWidth: 2 },
        label: {
          show: true,
          position: 'center',
          formatter: () => `{total|${formatNumber(summary.total)}}\n{sub|com validade}`,
          rich: {
            total: { fontSize: 26, fontWeight: 'bold', color: 'hsl(var(--foreground))' },
            sub: { fontSize: 12, color: 'hsl(var(--muted-foreground))', padding: [4, 0, 0, 0] },
          },
        },
        emphasis: { label: { show: true } },
        labelLine: { show: false },
        data: slices.map((s) => ({ name: s.name, value: s.value })),
      },
    ],
  };
}

const EMPTY_OPTION: EChartsCoreOption = {};

/** A legend row with the slice color, label and count — mirrors the ECharts slices, with counts. */
function DonutLegend({ summary }: { summary: ExpirySummary }) {
  return (
    <ul className="grid grid-cols-3 gap-2 text-center text-xs">
      {slicesOf(summary).map((slice) => (
        <li key={slice.name} className="flex flex-col items-center gap-1">
          <span className="flex items-center gap-1.5 text-muted-foreground">
            <span
              className="inline-block size-2.5 rounded-full"
              style={{ backgroundColor: slice.color }}
            />
            {slice.name}
          </span>
          <span className="text-sm font-semibold">{formatNumber(slice.value)}</span>
        </li>
      ))}
    </ul>
  );
}

export function ExpiryDonutChart({ summary, loading }: ExpiryDonutChartProps) {
  const isEmpty = !loading && (!summary || summary.total === 0);
  const option = useMemo(
    () => (summary && summary.total > 0 ? buildDonutOption(summary) : EMPTY_OPTION),
    [summary],
  );

  return (
    <ChartCard
      title="Situação de validade"
      description="Itens com validade registrada, por faixa."
      option={option}
      loading={loading}
      isEmpty={isEmpty}
      emptyLabel="Nenhum item com validade registrada."
      height={260}
      actions={
        <Link
          to="/inventory"
          className="text-sm font-medium text-primary underline-offset-4 hover:underline"
        >
          Ver estoque
        </Link>
      }
      footer={summary && summary.total > 0 ? <DonutLegend summary={summary} /> : undefined}
    />
  );
}
