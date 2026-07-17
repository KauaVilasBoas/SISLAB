/**
 * Presentation helpers for the cost report (card [E4] #109). Kept out of the page component so the pure
 * ECharts option building and label formatting stay reusable and unit-testable, and the page stays a
 * declarative composition (the same split the dashboard uses in dashboard-presentation.ts).
 */
import type { EChartsCoreOption } from '@/shared/lib/echarts';
import { formatCurrencyBrl } from '@/shared/lib/format';
import { STATUS_COLORS } from '@/modules/dashboard/components/dashboard-presentation';
import type { ExperimentCostItem, MonthlyCostItem } from '@/modules/inventory/types';

/** Abbreviated pt-BR month names indexed 0..11 — the x-axis labels of the "Custo por mês" bar chart. */
const MONTH_ABBREVIATIONS = [
  'Jan',
  'Fev',
  'Mar',
  'Abr',
  'Mai',
  'Jun',
  'Jul',
  'Ago',
  'Set',
  'Out',
  'Nov',
  'Dez',
] as const;

/** "Jan", "Fev"... for an ISO "YYYY-MM-DD" month key (parsed as a local date, no timezone shift). */
export function monthLabel(isoMonth: string): string {
  const [year, month] = isoMonth.split('-').map(Number);
  const index = (month ?? 1) - 1;
  return MONTH_ABBREVIATIONS[index] ?? String(year ?? isoMonth);
}

/** Short label for an experiment bucket: "Sem experimento" for the null bucket, else a shortened id. */
export function experimentLabel(experimentId: string | null): string {
  if (!experimentId) return 'Sem experimento';
  return `Exp. ${experimentId.slice(0, 8)}`;
}

/** BRL axis/tooltip formatter shared by both charts (compact "R$ 1.234,56"). */
function brl(value: number): string {
  return formatCurrencyBrl(value);
}

/**
 * Vertical bar option for "Custo por mês". The backend returns newest-first; the chart reads
 * chronologically (oldest → newest, left → right), so the series is reversed here.
 */
export function buildCostByMonthOption(items: readonly MonthlyCostItem[]): EChartsCoreOption {
  const chronological = [...items].reverse();
  return {
    tooltip: {
      trigger: 'axis',
      valueFormatter: (value: number | string) => brl(Number(value)),
    },
    grid: { left: 8, right: 16, top: 16, bottom: 24, containLabel: true },
    color: [STATUS_COLORS.info],
    xAxis: {
      type: 'category',
      data: chronological.map((item) => monthLabel(item.month)),
    },
    yAxis: {
      type: 'value',
      axisLabel: { formatter: (value: number) => brl(value) },
    },
    series: [
      {
        name: 'Custo',
        type: 'bar',
        barMaxWidth: 32,
        data: chronological.map((item) => item.totalCost),
      },
    ],
  };
}

/**
 * Horizontal bar option for "Custo por experimento". The backend returns highest-spend first; a horizontal
 * bar renders bottom-up, so the series is reversed to put the biggest spender at the top.
 */
export function buildCostByExperimentOption(
  items: readonly ExperimentCostItem[],
): EChartsCoreOption {
  const ascending = [...items].reverse();
  return {
    tooltip: {
      trigger: 'axis',
      valueFormatter: (value: number | string) => brl(Number(value)),
    },
    grid: { left: 8, right: 24, top: 16, bottom: 24, containLabel: true },
    color: [STATUS_COLORS.ok],
    xAxis: {
      type: 'value',
      axisLabel: { formatter: (value: number) => brl(value) },
    },
    yAxis: {
      type: 'category',
      data: ascending.map((item) => experimentLabel(item.experimentId)),
    },
    series: [
      {
        name: 'Custo',
        type: 'bar',
        barMaxWidth: 24,
        data: ascending.map((item) => item.totalCost),
      },
    ],
  };
}
