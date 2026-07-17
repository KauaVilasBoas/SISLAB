import type { BadgeProps } from '@/shared/components/ui/badge';
import type {
  ExpiryStatus,
  StockItemListItem,
  StockMovementType,
  StorageLocationType,
} from '@/modules/inventory/types';

/** Formats month-granularity validity (year, month) as "MM/YYYY", or a dash when absent. */
export function formatExpiry(year: number | null, month: number | null): string {
  if (year === null || month === null) return '—';
  return `${String(month).padStart(2, '0')}/${year}`;
}

/** Human label + badge variant for the derived expiry status, for both table and detail. */
export function expiryStatusPresentation(status: ExpiryStatus): {
  label: string;
  variant: NonNullable<BadgeProps['variant']>;
} {
  switch (status) {
    case 'Expired':
      return { label: 'Vencido', variant: 'default' };
    case 'ExpiringSoon':
      return { label: 'A vencer', variant: 'secondary' };
    case 'Ok':
      return { label: 'OK', variant: 'outline' };
    case 'NotApplicable':
    default:
      return { label: 'N/A', variant: 'muted' };
  }
}

/**
 * Colour + tooltip for the small expiry status dot the table renders next to the validity date, so a
 * scanning operator spots at-risk lots without reading the badge. Mirrors the badge classification:
 * red = expired, amber = expiring soon, green = ok, neutral = not applicable.
 */
export function expiryDotPresentation(status: ExpiryStatus): {
  className: string;
  title: string;
} {
  switch (status) {
    case 'Expired':
      return { className: 'bg-destructive', title: 'Vencido' };
    case 'ExpiringSoon':
      return { className: 'bg-amber-500', title: 'A vencer' };
    case 'Ok':
      return { className: 'bg-emerald-500', title: 'Validade em dia' };
    case 'NotApplicable':
    default:
      return { className: 'bg-muted-foreground/40', title: 'Sem validade' };
  }
}

/**
 * Classifies a batch's month-granularity validity (year, month) into the shared {@link ExpiryStatus}, so the
 * consumption lot picker (card [E7] #111) can colour each lot with the SAME semantics the table uses —
 * red = expired, amber = expiring soon, neutral = no validity. Unlike the item listing (whose status the
 * backend derives), a batch row only carries year/month, so the picker derives it client-side from the
 * current month: a validity strictly before this month is `Expired`, within `warningMonths` ahead is
 * `ExpiringSoon`, otherwise `Ok`. Kept intentionally coarse (month precision) — the backend remains the
 * authority; this only drives a non-blocking visual cue (expiry only alerts, never blocks consumption).
 */
export function batchExpiryStatus(
  year: number | null,
  month: number | null,
  warningMonths = 1,
): ExpiryStatus {
  if (year === null || month === null) return 'NotApplicable';

  const now = new Date();
  const currentMonths = now.getFullYear() * 12 + now.getMonth(); // month is 0-based
  const batchMonths = year * 12 + (month - 1);
  const delta = batchMonths - currentMonths;

  if (delta < 0) return 'Expired';
  if (delta <= warningMonths) return 'ExpiringSoon';
  return 'Ok';
}

/** Portuguese label for the aggregate's container state ("Open"/"Closed"). */
export function containerStateLabel(state: string): string {
  switch (state) {
    case 'Open':
      return 'Aberto';
    case 'Closed':
      return 'Fechado';
    default:
      return state;
  }
}

/** Formats a quantity + unit pair, trimming trailing zeros the aggregate may carry. */
export function formatQuantity(amount: number, unit: string): string {
  const normalized = Number.isInteger(amount)
    ? String(amount)
    : String(Number(amount.toFixed(3)));
  return `${normalized} ${unit}`;
}

/** True when the item's validity puts it in a state the UI highlights as at-risk. */
export function isExpiryAtRisk(item: StockItemListItem): boolean {
  return item.expiryStatus === 'Expired' || item.expiryStatus === 'ExpiringSoon';
}

/** Portuguese label + badge variant per movement type, for the ledger table and its filter. */
export function movementTypePresentation(type: StockMovementType): {
  label: string;
  variant: NonNullable<BadgeProps['variant']>;
} {
  switch (type) {
    case 'Received':
      return { label: 'Entrada', variant: 'default' };
    case 'Consumed':
      return { label: 'Consumo', variant: 'secondary' };
    case 'Transferred':
      return { label: 'Transferência', variant: 'outline' };
    case 'Disposed':
      return { label: 'Descarte', variant: 'muted' };
    default:
      return { label: type, variant: 'muted' };
  }
}

/** The four movement types in the order the filter dropdown lists them. */
export const MOVEMENT_TYPES: StockMovementType[] = [
  'Received',
  'Consumed',
  'Transferred',
  'Disposed',
];

/** Portuguese label for a storage location type, from the real LAFTE layout (card [E7] #112). */
export function storageLocationTypeLabel(type: StorageLocationType): string {
  switch (type) {
    case 'GeneralStorage':
      return 'Almoxarifado';
    case 'ReagentCabinet':
      return 'Armário de reagentes';
    case 'Refrigerated':
      return 'Refrigerado';
    case 'Controlled':
      return 'Controlados';
    case 'Partner':
      return 'Parceiros';
    default:
      return type;
  }
}

/** The storage location types in the order the create dropdown lists them. */
export const STORAGE_LOCATION_TYPES: StorageLocationType[] = [
  'GeneralStorage',
  'ReagentCabinet',
  'Refrigerated',
  'Controlled',
  'Partner',
];
