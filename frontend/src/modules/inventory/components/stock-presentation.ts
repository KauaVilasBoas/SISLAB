import type { BadgeProps } from '@/shared/components/ui/badge';
import type {
  ExpiryStatus,
  StockItemListItem,
  StockMovementType,
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
