import { encodeStockItemQr, encodeStorageLocationQr } from '@/shared/lib/qr';
import type { LocationSummaryItem, StockItemListItem } from '@/modules/inventory/types';

/**
 * The kind of aggregate a label points at — used only to key the selection and to colour the small
 * "Item"/"Local" tag printed on each sticker so an operator can tell the two apart on the cabinet.
 */
export type LabelKind = 'item' | 'location';

/**
 * A print-ready label, decoupled from the Inventory read models. The QR component only ever sees this
 * flat shape: `payload` is the exact SISLAB QR string to encode (built through the shared grammar, so
 * generation and scanning can never drift), `title` is the bold human-readable line, and `subtitle` is
 * the secondary line (lot for items, location type for locations). `selectionKey` is a stable, kind-
 * namespaced id used as the React key and as the selection-set member.
 */
export interface LabelSpec {
  selectionKey: string;
  kind: LabelKind;
  payload: string;
  title: string;
  subtitle: string | null;
}

/** The kind-namespaced key of a stock-item / storage-location label, so item and location ids never collide. */
export function itemSelectionKey(stockItemId: string): string {
  return `item:${stockItemId}`;
}

export function locationSelectionKey(storageLocationId: string): string {
  return `location:${storageLocationId}`;
}

/**
 * Builds the label for a stock item: its QR encodes `sislab:item:<guid>` (opens the quick baixa directly
 * on scan) and the readable text is the item name with its current lot underneath, so the operator can
 * confirm the sticker matches the physical container before gluing it.
 */
export function toItemLabel(item: StockItemListItem): LabelSpec {
  const lot = item.lotCode?.trim();
  return {
    selectionKey: itemSelectionKey(item.id),
    kind: 'item',
    payload: encodeStockItemQr(item.id),
    title: item.name,
    subtitle: lot ? `Lote ${lot}` : null,
  };
}

/**
 * Builds the label for a storage location: its QR encodes `sislab:location:<guid>` (opens the location's
 * item list on scan) and the readable text is the location name with its type underneath (e.g. the
 * controlled box), so the cabinet is unambiguous.
 */
export function toLocationLabel(location: LocationSummaryItem): LabelSpec {
  const type = location.type?.trim();
  return {
    selectionKey: locationSelectionKey(location.id),
    kind: 'location',
    payload: encodeStorageLocationQr(location.id),
    title: location.name,
    subtitle: type || null,
  };
}
