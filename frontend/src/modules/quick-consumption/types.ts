/**
 * Quick-consumption (mobile QR) read/write contracts (card [E7] #63).
 *
 * Mirrors the backend read model `StockItemDetail` (GET /api/inventory/stock-items/{id}) the flow loads
 * after scanning a `sislab:item:<guid>` QR — the "item card" (name, lot, on-hand balance, unit, storage
 * location) plus the controlled flag. Kept flat and primitive: the UI never sees the StockItem aggregate.
 * The write body reuses the shared RegisterConsumptionRequest from the inventory module.
 */

/**
 * A single stock item as returned by GET /api/inventory/stock-items/{id}. This is the card the mobile flow
 * renders after a scan: `quantity`/`unit` are the on-hand balance in the item's canonical unit (the stepper
 * operates in this unit), `lot` is the current lot (null when the item was received without one), and
 * `storageLocationName` backs the "QR lido — <armário>" banner.
 */
export interface StockItemDetail {
  id: string;
  name: string;
  category: string;
  quantity: number;
  unit: string;
  minimumQuantity: number;
  minimumUnit: string;
  expiryYear: number | null;
  expiryMonth: number | null;
  storageLocationId: string;
  storageLocationName: string | null;
  isControlled: boolean;
  companyId: string;
  lot: string | null;
}
