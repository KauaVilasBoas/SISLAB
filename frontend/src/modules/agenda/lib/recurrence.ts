/**
 * RFC 5545 RRULE helpers for the Google-Calendar-style recurrence UX (card [E10.6]).
 *
 * The backend stores and expands a plain RRULE string; the form never invents its own recurrence engine.
 * These helpers translate between that string and the small structured model the form edits, covering the
 * presets (daily / weekly-on-day / weekday / monthly / annually) and the "custom" builder.
 */

export type Frequency = 'DAILY' | 'WEEKLY' | 'MONTHLY' | 'YEARLY';

export type EndMode = 'never' | 'onDate' | 'afterCount';

/** RFC 5545 weekday tokens, Monday-first to match the weekday-checkbox order in the custom modal. */
export const WEEKDAYS = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU'] as const;
export type Weekday = (typeof WEEKDAYS)[number];

export const WEEKDAY_LABEL: Record<Weekday, string> = {
  MO: 'Seg',
  TU: 'Ter',
  WE: 'Qua',
  TH: 'Qui',
  FR: 'Sex',
  SA: 'Sáb',
  SU: 'Dom',
};

/** The structured recurrence the custom builder edits. `null` means "does not repeat". */
export interface CustomRecurrence {
  frequency: Frequency;
  interval: number;
  /** Selected weekdays (WEEKLY only). */
  byDay: Weekday[];
  endMode: EndMode;
  /** 'YYYY-MM-DD' when endMode === 'onDate'. */
  until: string | null;
  /** Occurrence count when endMode === 'afterCount'. */
  count: number | null;
}

/** The date's weekday as an RFC 5545 token (from a 'YYYY-MM-DD' string, parsed as local noon to avoid TZ drift). */
export function weekdayOf(isoDate: string): Weekday {
  const date = new Date(`${isoDate}T12:00:00`);
  // getDay(): 0=Sun..6=Sat; map to Monday-first WEEKDAYS index.
  const mondayFirst = (date.getDay() + 6) % 7;
  return WEEKDAYS[mondayFirst];
}

/** The date's day-of-month (1..31) from a 'YYYY-MM-DD' string. */
export function dayOfMonth(isoDate: string): number {
  return Number(isoDate.slice(8, 10));
}

/**
 * Formats a structured recurrence as an RRULE string, or `null` when it does not repeat. UNTIL is emitted as a
 * UTC end-of-day timestamp so the last occurrence's whole day is inclusive.
 */
export function toRRule(recurrence: CustomRecurrence | null): string | null {
  if (recurrence === null) return null;

  const parts: string[] = [`FREQ=${recurrence.frequency}`];

  if (recurrence.interval > 1) parts.push(`INTERVAL=${recurrence.interval}`);

  if (recurrence.frequency === 'WEEKLY' && recurrence.byDay.length > 0) {
    parts.push(`BYDAY=${recurrence.byDay.join(',')}`);
  }

  if (recurrence.endMode === 'onDate' && recurrence.until) {
    parts.push(`UNTIL=${recurrence.until.replace(/-/g, '')}T235959Z`);
  } else if (recurrence.endMode === 'afterCount' && recurrence.count) {
    parts.push(`COUNT=${recurrence.count}`);
  }

  return parts.join(';');
}

/** Parses an RRULE string into the structured model, tolerating unknown parts. Returns `null` for empty input. */
export function fromRRule(rrule: string | null): CustomRecurrence | null {
  if (!rrule) return null;

  const map = new Map<string, string>();
  for (const part of rrule.split(';')) {
    const [key, value] = part.split('=');
    if (key && value) map.set(key.toUpperCase(), value);
  }

  const frequency = (map.get('FREQ') as Frequency) ?? 'DAILY';
  const interval = Number(map.get('INTERVAL') ?? '1') || 1;
  const byDay = (map.get('BYDAY')?.split(',') ?? []).filter((d): d is Weekday =>
    (WEEKDAYS as readonly string[]).includes(d),
  );

  let endMode: EndMode = 'never';
  let until: string | null = null;
  let count: number | null = null;

  if (map.has('UNTIL')) {
    endMode = 'onDate';
    const raw = map.get('UNTIL')!; // e.g. 20260930T235959Z
    until = `${raw.slice(0, 4)}-${raw.slice(4, 6)}-${raw.slice(6, 8)}`;
  } else if (map.has('COUNT')) {
    endMode = 'afterCount';
    count = Number(map.get('COUNT')) || 1;
  }

  return { frequency, interval, byDay, endMode, until, count };
}
