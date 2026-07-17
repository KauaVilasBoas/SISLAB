/**
 * pt-BR formatting helpers, colocated in shared/lib so every module reuses them.
 */

const dateTimeFormatter = new Intl.DateTimeFormat('pt-BR', {
  dateStyle: 'short',
  timeStyle: 'short',
});

const dateFormatter = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short' });

const numberFormatter = new Intl.NumberFormat('pt-BR');

const currencyFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
});

const relativeTimeFormatter = new Intl.RelativeTimeFormat('pt-BR', {
  numeric: 'auto',
  style: 'long',
});

/** Descending [unit, seconds-per-unit] thresholds the relative formatter walks from largest to smallest. */
const RELATIVE_TIME_UNITS: ReadonlyArray<readonly [Intl.RelativeTimeFormatUnit, number]> =
  [
    ['year', 31_536_000],
    ['month', 2_592_000],
    ['week', 604_800],
    ['day', 86_400],
    ['hour', 3600],
    ['minute', 60],
  ] as const;

export function formatDateTime(value: string | Date | null | undefined): string {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : dateTimeFormatter.format(date);
}

export function formatDate(value: string | Date | null | undefined): string {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : dateFormatter.format(date);
}

export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return numberFormatter.format(value);
}

/** pt-BR currency ("R$ 1.234,56"), or a dash when the value is absent (e.g. an uncosted movement). */
export function formatCurrencyBrl(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—';
  return currencyFormatter.format(value);
}

/**
 * Human, pt-BR relative time ("há 2 horas", "há 3 dias"). Picks the largest fitting unit and, under a
 * minute, collapses to "agora mesmo". Falls back to the absolute short date/time for invalid input so a
 * bad timestamp never renders as "NaN". Used by the notification list (card [E7] #65) for the timestamp.
 */
export function formatRelativeTime(value: string | Date | null | undefined): string {
  if (!value) return '—';
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return '—';

  const diffInSeconds = Math.round((date.getTime() - Date.now()) / 1000);
  const absSeconds = Math.abs(diffInSeconds);

  if (absSeconds < 60) return 'agora mesmo';

  for (const [unit, secondsPerUnit] of RELATIVE_TIME_UNITS) {
    if (absSeconds >= secondsPerUnit) {
      return relativeTimeFormatter.format(
        Math.round(diffInSeconds / secondsPerUnit),
        unit,
      );
    }
  }

  return dateTimeFormatter.format(date);
}
