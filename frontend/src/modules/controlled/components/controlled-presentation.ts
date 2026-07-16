import type { BadgeProps } from '@/shared/components/ui/badge';
import type { AuditTrailEntry } from '@/modules/controlled/types';

/**
 * Presentation helpers for the Controlados compliance screen (card [E7] #62): the audit action
 * vocabulary rendered in the trail and a tolerant parser for the writer's JSON payload, which differs
 * per action (a count carries counted/divergence, a disposal carries a reason, a consumption a
 * quantity). Kept UI-free so both the trail table and its tests can reuse them.
 */

/** Human label + badge variant for an audit action, for the trail's "Ação" column. */
export function auditActionPresentation(action: string): {
  label: string;
  variant: NonNullable<BadgeProps['variant']>;
} {
  switch (action) {
    case 'consumption':
      return { label: 'Consumo', variant: 'secondary' };
    case 'disposal':
      return { label: 'Descarte', variant: 'muted' };
    case 'stock-count':
      return { label: 'Conferência', variant: 'default' };
    default:
      return { label: action, variant: 'outline' };
  }
}

/** The compliance-relevant fields extracted from an audit entry's per-action JSON payload. */
export interface AuditPayloadSummary {
  /** The amount involved (consumed/disposed) or the counted quantity for a conference. */
  quantity: number | null;
  unit: string | null;
  /** Only for a conference: counted minus system balance (may be zero, negative or positive). */
  divergence: number | null;
  /** Only for a disposal: the audited justification. */
  reason: string | null;
}

/** Case-insensitive lookup over the payload object (the writer serializes PascalCase properties). */
function readNumber(source: Record<string, unknown>, ...keys: string[]): number | null {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === 'number' && Number.isFinite(value)) return value;
  }
  return null;
}

function readString(source: Record<string, unknown>, ...keys: string[]): string | null {
  for (const key of keys) {
    const value = source[key];
    if (typeof value === 'string' && value.length > 0) return value;
  }
  return null;
}

/**
 * Parses an audit entry's raw JSON payload into the compliance summary the trail renders. Defensive by
 * design: a malformed or unexpected payload yields an all-null summary rather than throwing, so one bad
 * row never breaks the trail.
 */
export function parseAuditPayload(entry: AuditTrailEntry): AuditPayloadSummary {
  let source: Record<string, unknown>;
  try {
    const parsed: unknown = JSON.parse(entry.payload);
    source = typeof parsed === 'object' && parsed !== null ? (parsed as Record<string, unknown>) : {};
  } catch {
    return { quantity: null, unit: null, divergence: null, reason: null };
  }

  return {
    quantity: readNumber(source, 'CountedQuantity', 'Quantity'),
    unit: readString(source, 'Unit'),
    divergence: readNumber(source, 'Divergence'),
    reason: readString(source, 'Reason'),
  };
}

/** Formats a numeric quantity + unit pair for the trail, trimming trailing zeros. */
export function formatAmount(quantity: number | null, unit: string | null): string {
  if (quantity === null) return '—';
  const normalized = Number.isInteger(quantity)
    ? String(quantity)
    : String(Number(quantity.toFixed(3)));
  return unit ? `${normalized} ${unit}` : normalized;
}

/** A signed, human-readable divergence (e.g. "+2 mL", "0", "−1.5 mL"), or a dash when not applicable. */
export function formatDivergence(divergence: number | null, unit: string | null): string {
  if (divergence === null) return '—';
  if (divergence === 0) return unit ? `0 ${unit}` : '0';
  const magnitude = Number.isInteger(divergence)
    ? String(Math.abs(divergence))
    : String(Number(Math.abs(divergence).toFixed(3)));
  const sign = divergence > 0 ? '+' : '−';
  return unit ? `${sign}${magnitude} ${unit}` : `${sign}${magnitude}`;
}
