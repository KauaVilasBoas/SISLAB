import type { ReactNode } from 'react';
import ReactEChartsCore from 'echarts-for-react/lib/core';
import { echarts, type EChartsCoreOption } from '@/shared/lib/echarts';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/components/ui/card';
import { cn } from '@/shared/lib/utils';

interface ChartCardProps {
  title: string;
  description?: string;
  /** ECharts option object. See https://echarts.apache.org/en/option.html */
  option: EChartsCoreOption;
  loading?: boolean;
  /** Chart canvas height in pixels. */
  height?: number;
  /** Renders an empty state instead of the chart when true. */
  isEmpty?: boolean;
  emptyLabel?: string;
  className?: string;
  /** Optional controls rendered on the right of the header (e.g. period tabs, a "Ver estoque" link). */
  actions?: ReactNode;
  /** Optional content rendered below the chart canvas (e.g. a legend with counts). */
  footer?: ReactNode;
}

/**
 * Reusable chart container: a shadcn Card wrapping a tree-shaken ECharts canvas.
 * Module components pass a fully-built `option`; this handles loading/empty/resize.
 */
export function ChartCard({
  title,
  description,
  option,
  loading = false,
  height = 320,
  isEmpty = false,
  emptyLabel = 'Sem dados para exibir.',
  className,
  actions,
  footer,
}: ChartCardProps) {
  return (
    <Card className={cn('overflow-hidden', className)}>
      <CardHeader className="flex flex-row items-start justify-between gap-4 space-y-0">
        <div className="space-y-1.5">
          <CardTitle>{title}</CardTitle>
          {description ? <CardDescription>{description}</CardDescription> : null}
        </div>
        {actions ? <div className="flex shrink-0 items-center gap-2">{actions}</div> : null}
      </CardHeader>
      <CardContent>
        {isEmpty ? (
          <div
            className="flex items-center justify-center text-sm text-muted-foreground"
            style={{ height }}
          >
            {emptyLabel}
          </div>
        ) : (
          <ReactEChartsCore
            echarts={echarts}
            option={option}
            showLoading={loading}
            notMerge
            lazyUpdate
            style={{ height }}
          />
        )}
        {footer ? <div className="mt-4">{footer}</div> : null}
      </CardContent>
    </Card>
  );
}
