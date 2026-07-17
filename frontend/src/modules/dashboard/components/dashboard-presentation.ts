/**
 * Presentation helpers for the dashboard widgets — the period tabs of the activity chart and the
 * status colors shared by the KPI cards and the validity donut. Kept out of the components so the
 * pure mapping/date math is reusable and testable, and the components stay declarative.
 */

/** The three approved activity-chart windows (card [E7] #49): 7 days, 30 days, 3 months. */
export type ActivityPeriodKey = '7d' | '30d' | '3m';

export interface ActivityPeriod {
  key: ActivityPeriodKey;
  label: string;
  /** Inclusive window width in days (drives the [from, to] range the series is fetched for). */
  days: number;
}

/** Ordered period tabs — 30 days is the default (first render), matching the prototype. */
export const ACTIVITY_PERIODS: readonly ActivityPeriod[] = [
  { key: '7d', label: '7 dias', days: 7 },
  { key: '30d', label: '30 dias', days: 30 },
  { key: '3m', label: '3 meses', days: 90 },
] as const;

export const DEFAULT_ACTIVITY_PERIOD: ActivityPeriodKey = '30d';

/** ISO yyyy-MM-dd for `daysAgo` days before today (0 = today), in local time. */
export function isoDaysAgo(daysAgo: number): string {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  return toIsoDate(d);
}

/** The inclusive [from, to] ISO window for a given period: `days-1` back through today. */
export function windowFor(period: ActivityPeriod): { from: string; to: string } {
  return { from: isoDaysAgo(period.days - 1), to: isoDaysAgo(0) };
}

function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * The status palette as literal HSL strings (mirrors the `--status-*` CSS tokens). ECharts needs a
 * concrete color value at build time — it cannot read a CSS custom property — so the donut and the
 * activity line reference these directly, keeping them visually in sync with the Tailwind theme.
 */
export const STATUS_COLORS = {
  expired: 'hsl(0, 72%, 51%)',
  warning: 'hsl(38, 92%, 50%)',
  ok: 'hsl(142, 71%, 45%)',
  info: 'hsl(217, 91%, 60%)',
} as const;

/**
 * The time-of-day greeting ("Bom dia/Bom tarde/Boa noite"). Extracted so the header stays a pure
 * render and the boundary hours are unit-testable.
 */
export function greetingFor(hour: number): string {
  if (hour < 12) return 'Bom dia';
  if (hour < 18) return 'Boa tarde';
  return 'Boa noite';
}
