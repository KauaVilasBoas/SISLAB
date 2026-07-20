/**
 * Date/range helpers for the calendar views (card [E10.5]). All calendar data is keyed by a local 'YYYY-MM-DD';
 * event instants come from the API as UTC ISO strings and are rendered in the browser's local zone.
 */

export type CalendarView = 'day' | 'week' | 'month' | 'rooms';

/** Local 'YYYY-MM-DD' for a Date (not UTC — the calendar grid is a local-day grid). */
export function toIsoDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

export function todayIso(): string {
  return toIsoDate(new Date());
}

/** Parses a local 'YYYY-MM-DD' into a Date at local midnight. */
export function parseIsoDate(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

export function addDays(iso: string, days: number): string {
  const date = parseIsoDate(iso);
  date.setDate(date.getDate() + days);
  return toIsoDate(date);
}

export function addMonths(iso: string, months: number): string {
  const date = parseIsoDate(iso);
  date.setMonth(date.getMonth() + months);
  return toIsoDate(date);
}

/** Monday of the week containing `iso`. */
export function weekStart(iso: string): string {
  const date = parseIsoDate(iso);
  const mondayFirst = (date.getDay() + 6) % 7;
  date.setDate(date.getDate() - mondayFirst);
  return toIsoDate(date);
}

/** The seven local 'YYYY-MM-DD' of the week containing `iso`, Monday-first. */
export function weekDays(iso: string): string[] {
  const monday = weekStart(iso);
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

/**
 * The inclusive [start, end] 'YYYY-MM-DD' range to request from GET /api/agenda/calendar for a view anchored on
 * `iso`. Month view is padded to whole weeks so the grid's leading/trailing days are populated.
 */
export function rangeForView(view: CalendarView, iso: string): { start: string; end: string } {
  // 'rooms' is a single-day occupancy view fed by its own query; a day-range keeps callers total.
  if (view === 'day' || view === 'rooms') return { start: iso, end: iso };
  if (view === 'week') {
    const days = weekDays(iso);
    return { start: days[0], end: days[6] };
  }
  // month: pad to whole weeks around the calendar month.
  const anchor = parseIsoDate(iso);
  const firstOfMonth = toIsoDate(new Date(anchor.getFullYear(), anchor.getMonth(), 1));
  const lastOfMonth = toIsoDate(new Date(anchor.getFullYear(), anchor.getMonth() + 1, 0));
  return { start: weekStart(firstOfMonth), end: addDays(weekStart(lastOfMonth), 6) };
}

/** The 6×7 (or 5×7) grid of local 'YYYY-MM-DD' spanning the padded month, Monday-first. */
export function monthGrid(iso: string): string[] {
  const { start, end } = rangeForView('month', iso);
  const days: string[] = [];
  for (let day = start; day <= end; day = addDays(day, 1)) days.push(day);
  return days;
}

/** Local 'YYYY-MM-DD' of a UTC ISO instant (the day the occurrence renders on in the grid). */
export function localDateOf(isoUtc: string): string {
  return toIsoDate(new Date(isoUtc));
}

/** Local 'HH:mm' of a UTC ISO instant. */
export function localTime(isoUtc: string): string {
  return new Date(isoUtc).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
}

/** Minutes since local midnight for a UTC ISO instant — used to place an event on the day/week time grid. */
export function minutesSinceMidnight(isoUtc: string): number {
  const date = new Date(isoUtc);
  return date.getHours() * 60 + date.getMinutes();
}

/** A human range label for the current view, e.g. "12 – 18 de mai" or "maio de 2026". */
export function viewTitle(view: CalendarView, iso: string): string {
  const date = parseIsoDate(iso);
  if (view === 'day' || view === 'rooms') {
    return date.toLocaleDateString('pt-BR', { day: '2-digit', month: 'long', year: 'numeric' });
  }
  if (view === 'week') {
    const days = weekDays(iso);
    const from = parseIsoDate(days[0]);
    const to = parseIsoDate(days[6]);
    const fromLabel = from.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
    const toLabel = to.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
    return `${fromLabel} – ${toLabel}`;
  }
  return date.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
}
