import { useMemo } from 'react';
import ReactEChartsCore from 'echarts-for-react/lib/core';
import type {
  CustomSeriesRenderItemAPI,
  CustomSeriesRenderItemParams,
  CustomSeriesRenderItemReturn,
} from 'echarts';
import { echarts, type EChartsCoreOption } from '@/shared/lib/echarts';
import { Card, CardContent } from '@/shared/components/ui/card';
import { useRoomOccupancy } from '@/modules/agenda/api/entries.queries';
import { localTime, todayIso } from '@/modules/agenda/lib/calendar';
import type { RoomOccupancySlot } from '@/modules/agenda/types';

/**
 * Room-occupancy Gantt (card [E10.11]) for the calendar's "Salas" view. One lane per room on the Y axis, the
 * local 00:00–24:00 clock on the X axis, and every {@link RoomOccupancySlot} rendered as a horizontal bar via an
 * ECharts Custom Series (`renderItem`). All slots are `RoomBooking` by contract, so bars are coloured per lane
 * (a stable colour per room) to tell rooms apart; overlapping slots in the same room are outlined in red. A
 * "now" marker is drawn only when the viewed day is today.
 */

const CONFLICT_STROKE = '#ef4444';
const BAR_HEIGHT_RATIO = 0.6;
const MIN_BAR_WIDTH_PX = 4;
const ROW_HEIGHT_PX = 60;
const MIN_CHART_HEIGHT_PX = 300;
const MS_PER_MINUTE = 60_000;

/** A stable, colour-blind-friendly palette; lanes cycle through it by room index. */
const LANE_PALETTE = [
  '#3b82f6', // blue
  '#10b981', // emerald
  '#f59e0b', // amber
  '#8b5cf6', // violet
  '#ec4899', // pink
  '#14b8a6', // teal
  '#f43f5e', // rose
  '#6366f1', // indigo
] as const;

/** A room lane on the Y axis, keyed by its (possibly null) room id and labelled with its display name. */
interface Lane {
  key: string;
  label: string;
}

/**
 * One Custom Series datum: `[laneIndex, startMs, endMs, isConflict]` plus the source slot carried through for the
 * tooltip. The first four are numeric/boolean so `api.value(i)`/`api.coord(...)` can position the bar.
 */
interface GanttDatum {
  value: [number, number, number, boolean];
  slot: RoomOccupancySlot;
  laneLabel: string;
}

/** Two slots conflict when they share a room and their [start, end) intervals overlap. */
function hasConflict(a: RoomOccupancySlot, b: RoomOccupancySlot): boolean {
  return (
    a.roomId === b.roomId &&
    new Date(a.startUtc) < new Date(b.endUtc) &&
    new Date(b.startUtc) < new Date(a.endUtc)
  );
}

/** Local midnight (start of the viewed day) as epoch ms — the X axis min. */
function localDayStartMs(isoDate: string): number {
  const [y, m, d] = isoDate.split('-').map(Number);
  return new Date(y, m - 1, d, 0, 0, 0, 0).getTime();
}

/** Stable, insertion-ordered lanes (one per distinct room) from the day's slots. */
function buildLanes(slots: RoomOccupancySlot[]): Lane[] {
  const lanes: Lane[] = [];
  const seen = new Set<string>();
  for (const slot of slots) {
    const key = slot.roomId ?? '__no_room__';
    if (seen.has(key)) continue;
    seen.add(key);
    lanes.push({ key, label: slot.roomName ?? 'Sem sala' });
  }
  return lanes;
}

export interface RoomOccupancyGanttProps {
  /** The day to render, as a local 'YYYY-MM-DD'. */
  date: string;
}

export function RoomOccupancyGantt({ date }: RoomOccupancyGanttProps) {
  const { data: slots, isLoading } = useRoomOccupancy(date);

  const lanes = useMemo(() => buildLanes(slots ?? []), [slots]);

  const data = useMemo<GanttDatum[]>(() => {
    if (!slots) return [];
    const laneIndex = new Map(lanes.map((lane, index) => [lane.key, index]));
    return slots.map((slot) => {
      const key = slot.roomId ?? '__no_room__';
      const isConflict = slots.some((other) => other !== slot && hasConflict(slot, other));
      return {
        value: [
          laneIndex.get(key) ?? 0,
          new Date(slot.startUtc).getTime(),
          new Date(slot.endUtc).getTime(),
          isConflict,
        ],
        slot,
        laneLabel: slot.roomName ?? 'Sem sala',
      };
    });
  }, [slots, lanes]);

  const chartHeight = Math.max(MIN_CHART_HEIGHT_PX, lanes.length * ROW_HEIGHT_PX);

  const option = useMemo<EChartsCoreOption>(() => {
    const dayStart = localDayStartMs(date);
    const dayEnd = dayStart + 24 * 60 * MS_PER_MINUTE;
    const isToday = date === todayIso();

    return {
      tooltip: {
        // Data-item tooltip; the formatter reads the carried slot off params.data.
        formatter: (params: unknown) => {
          const datum = (params as { data?: GanttDatum }).data;
          if (!datum) return '';
          const { slot } = datum;
          return [
            `<strong>${escapeHtml(slot.title)}</strong>`,
            `Sala: ${escapeHtml(datum.laneLabel)}`,
            slot.responsibleName ? `Responsável: ${escapeHtml(slot.responsibleName)}` : null,
            `${localTime(slot.startUtc)} – ${localTime(slot.endUtc)}`,
          ]
            .filter(Boolean)
            .join('<br/>');
        },
      },
      grid: { left: 140, right: 24, top: 16, bottom: 40 },
      xAxis: {
        type: 'time',
        min: dayStart,
        max: dayEnd,
        interval: 3 * 60 * MS_PER_MINUTE, // a gridline every 3h keeps a full day legible
        axisLabel: { formatter: (value: number) => localTime(new Date(value).toISOString()) },
        splitLine: { show: true, lineStyle: { color: 'rgba(148, 163, 184, 0.2)' } },
      },
      yAxis: {
        type: 'category',
        data: lanes.map((lane) => lane.label),
        inverse: true, // first room on top
        axisTick: { show: false },
        splitLine: { show: true, lineStyle: { color: 'rgba(148, 163, 184, 0.12)' } },
      },
      series: [
        {
          type: 'custom',
          renderItem,
          encode: { x: [1, 2], y: 0 },
          data,
          ...(isToday
            ? {
                markLine: {
                  silent: true,
                  symbol: 'none',
                  lineStyle: { color: CONFLICT_STROKE, width: 1, type: 'solid' },
                  label: { formatter: 'Agora', position: 'insideEndTop', color: CONFLICT_STROKE },
                  data: [{ xAxis: Date.now() }],
                },
              }
            : {}),
        },
      ],
    };
  }, [date, lanes, data]);

  if (isLoading) return <GanttSkeleton />;

  if (!slots || slots.length === 0) {
    return (
      <Card>
        <CardContent className="flex h-64 items-center justify-center text-sm text-muted-foreground">
          Nenhuma ocupação de sala para este dia
        </CardContent>
      </Card>
    );
  }

  return (
    <Card>
      <CardContent className="pt-6">
        <ReactEChartsCore
          echarts={echarts}
          option={option}
          notMerge
          lazyUpdate
          style={{ width: '100%', height: chartHeight }}
        />
      </CardContent>
    </Card>
  );
}

/**
 * Draws one booking as a rectangle: X spans start→end (clamped to a minimum width so a very short slot stays
 * visible), Y is centred on the room lane at 60% of the lane height, and the fill cycles the lane palette. A
 * conflicting slot gets a red outline on top of its fill.
 */
function renderItem(
  _params: CustomSeriesRenderItemParams,
  api: CustomSeriesRenderItemAPI,
): CustomSeriesRenderItemReturn {
  const laneIndex = api.value(0) as number;
  const start = api.coord([api.value(1), laneIndex]);
  const end = api.coord([api.value(2), laneIndex]);
  const isConflict = api.value(3) as unknown as boolean;
  const laneHeight = (api.size?.([0, 1]) as number[])[1];
  const barHeight = laneHeight * BAR_HEIGHT_RATIO;
  const fill = LANE_PALETTE[laneIndex % LANE_PALETTE.length];

  return {
    type: 'rect',
    shape: {
      x: start[0],
      y: start[1] - barHeight / 2,
      width: Math.max(end[0] - start[0], MIN_BAR_WIDTH_PX),
      height: barHeight,
    },
    style: {
      fill,
      stroke: isConflict ? CONFLICT_STROKE : 'transparent',
      lineWidth: isConflict ? 2 : 0,
    },
    emphasis: { style: { opacity: 0.8 } },
  };
}

function GanttSkeleton() {
  return (
    <Card>
      <CardContent className="space-y-3 pt-6">
        {Array.from({ length: 5 }).map((_, row) => (
          <div key={row} className="flex items-center gap-3">
            <div className="h-4 w-28 shrink-0 animate-pulse rounded bg-muted" />
            <div
              className="h-6 animate-pulse rounded bg-muted"
              style={{ width: `${40 + ((row * 13) % 45)}%` }}
            />
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

/** Minimal HTML escaping for user-provided text injected into the rich-HTML tooltip. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
