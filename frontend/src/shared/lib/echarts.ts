/**
 * Tree-shaken Apache ECharts core.
 *
 * Register only the chart types and components the app uses so the bundle stays
 * small. Import `echarts` from here (not from 'echarts') and pass it to
 * ReactEChartsCore. Add a new chart/component here once and it's available app-wide.
 *
 * Docs & examples: https://echarts.apache.org/examples/en/index.html#chart-type-bar
 */
import * as echarts from 'echarts/core';
import { BarChart, CustomChart, LineChart, PieChart } from 'echarts/charts';
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DatasetComponent,
  MarkLineComponent,
} from 'echarts/components';
import { CanvasRenderer } from 'echarts/renderers';
import type { EChartsCoreOption } from 'echarts/core';

echarts.use([
  BarChart,
  CustomChart,
  LineChart,
  PieChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DatasetComponent,
  MarkLineComponent,
  CanvasRenderer,
]);

export { echarts };
export type { EChartsCoreOption };
