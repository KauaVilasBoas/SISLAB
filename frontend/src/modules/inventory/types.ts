/**
 * Inventory module read/write contracts (card [E7] #46).
 *
 * Mirrors the SISLAB Inventory backend read models (StockItemListItem, StockItemDetail,
 * LocationSummaryItem) and the write request bodies exposed by StockController, plus the two
 * Configuration catalogues the create form drives its dropdowns from (item categories, units).
 * Kept flat and primitive — the UI never sees the StockItem aggregate or its value objects.
 */

/** Derived expiry classification of a stock item, mirroring the backend ExpiryStatusView enum. */
export type ExpiryStatus = 'NotApplicable' | 'Ok' | 'ExpiringSoon' | 'Expired';

/** A stock item row as listed by the inventory table (GET /api/inventory/stock-items). */
export interface StockItemListItem {
  id: string;
  name: string;
  category: string;
  brand: string | null;
  lotCode: string | null;
  quantity: number;
  unit: string;
  minimumQuantity: number;
  minimumUnit: string;
  isBelowMinimum: boolean;
  expiryYear: number | null;
  expiryMonth: number | null;
  expiryStatus: ExpiryStatus;
  /** Container state label as stored by the aggregate, e.g. "Open" | "Closed". */
  containerState: string;
  isControlled: boolean;
  storageLocationId: string;
  storageLocationName: string | null;
  storageLocationType: string | null;
  application: string | null;
}

/** A storage location aggregate row for the location filter (GET /storage-locations/summary). */
export interface LocationSummaryItem {
  id: string;
  name: string;
  type: string;
  isActive: boolean;
  itemCount: number;
  expiredItemCount: number;
  isCritical: boolean;
}

/**
 * Kind of storage location, mirroring the backend StorageLocationType enum. Drives the type dropdown on the
 * management modal (fixed at creation) and its label rendering. `Refrigerated` is the only type that may
 * carry a target temperature range; `Controlled` is the only one allowed to hold controlled items.
 */
export type StorageLocationType =
  | 'GeneralStorage'
  | 'ReagentCabinet'
  | 'Refrigerated'
  | 'Controlled'
  | 'Partner';

/**
 * A storage location row for the management screen (GET /api/inventory/storage-locations). Flat by design:
 * it exposes the write-side editable fields the gestão form binds to — plus the current `isActive` flag and
 * the derived `itemCount` so the UI can warn before deactivating a location that still holds stock. Distinct
 * from {@link LocationSummaryItem} (the item-browser sidebar, which also derives the expired count and a
 * "critical" flag).
 */
export interface StorageLocationListItem {
  id: string;
  name: string;
  type: StorageLocationType;
  description: string | null;
  isActive: boolean;
  temperatureMinCelsius: number | null;
  temperatureMaxCelsius: number | null;
  itemCount: number;
}

/**
 * Request body to register a storage location (POST /api/inventory/storage-locations). The temperature bounds
 * are accepted only for a refrigerated location and must travel together (both set or both null).
 */
export interface RegisterStorageLocationRequest {
  name: string;
  type: StorageLocationType;
  description: string | null;
  temperatureMinCelsius: number | null;
  temperatureMaxCelsius: number | null;
}

/**
 * Request body to correct a storage location's metadata (PUT /api/inventory/storage-locations/{id}). The type
 * is intentionally absent — it is fixed at creation. A null/blank description clears it; null temperature
 * bounds clear the range.
 */
export interface UpdateStorageLocationRequest {
  name: string;
  description: string | null;
  temperatureMinCelsius: number | null;
  temperatureMaxCelsius: number | null;
}

/** A per-tenant item category for the create form dropdown (GET /configuration/item-categories). */
export interface ItemCategoryOption {
  id: string;
  name: string;
  aliases: string;
  isControlled: boolean;
}

/** A per-tenant unit of measure for the create form dropdown (GET /configuration/units). */
export interface UnitOption {
  id: string;
  symbol: string;
  name: string;
}

/** Filters applied to the stock item listing; empty values mean "no filter". */
export interface StockItemFilters {
  storageLocationId?: string;
  category?: string;
  search?: string;
}

/** Request body for registering a new stock item (POST /api/inventory/stock-items). */
export interface RegisterStockItemRequest {
  name: string;
  categoryId: string;
  storageLocationId: string;
  initialQuantity: number;
  minimumQuantity: number;
  unit: string;
  isControlled: boolean;
  brand: string | null;
  application: string | null;
  lotCode: string | null;
  expiryYear: number | null;
  expiryMonth: number | null;
}

/**
 * Request body to correct a stock item's metadata (PUT /api/inventory/stock-items/{id}).
 *
 * Conservative by design (card [E7] #46): only the non-balance fields are editable. The unit, lot, expiry
 * and on-hand quantity are intentionally absent — those change only through stock movements. The minimum
 * reuses the item's current unit. A null/blank brand or application clears it.
 */
export interface UpdateStockItemRequest {
  name: string;
  categoryId: string;
  storageLocationId: string;
  minimumQuantity: number;
  brand: string | null;
  application: string | null;
}

/** Request body for an incoming stock entry (POST /stock-items/{id}/entries). */
export interface RegisterStockEntryRequest {
  quantity: number;
  unit: string;
  lotCode: string | null;
  expiryYear: number | null;
  expiryMonth: number | null;
  supplierPartnerId: string | null;
  occurredOn: string | null;
  /**
   * The batch's unit price in BRL (cards #109/#110); null for donations / no-invoice items. Sent only by
   * users holding Inventory.Cost.Read — the cost field is gated, so operators without the permission never
   * see it and this stays null.
   */
  unitCostBrl: number | null;
}

/** Request body for a consumption (POST /stock-items/{id}/consumptions). */
export interface RegisterConsumptionRequest {
  quantity: number;
  unit: string;
  experimentId: string | null;
  occurredOn: string | null;
  /**
   * The lot the operator chose to draw from first (card [E7] #111). When null the backend draws the
   * balance FEFO automatically (first-expired-first-out) — the picker defaults to that behaviour.
   */
  preferredBatchId: string | null;
}

/** Request body for a transfer between storage locations (POST /stock-items/{id}/transfers). */
export interface TransferStockRequest {
  fromLocationId: string;
  toLocationId: string;
  occurredOn: string | null;
}

/** Request body for a disposal (POST /stock-items/{id}/disposals). */
export interface DisposeStockRequest {
  quantity: number;
  unit: string;
  reason: string;
  occurredOn: string | null;
}

/**
 * An available batch of a stock item (GET /api/inventory/stock-batches/{id}) — mirrors the backend
 * StockBatchItem. Feeds the consumption lot picker (card [E7] #111): each lot's code, month-granularity
 * validity, remaining balance and (cost-gated) unit cost. The list already arrives FEFO-ordered from the
 * backend (validity ascending, batches without an expiry last), so the first row is the FEFO default.
 */
export interface StockBatchItem {
  batchId: string;
  lotCode: string | null;
  expiryYear: number | null;
  expiryMonth: number | null;
  remainingQuantity: number;
  unit: string;
  /** Unit price in BRL; null for donations / no-invoice items. */
  unitCostBrl: number | null;
  /** When the batch was received, ISO UTC timestamp. */
  receivedAtUtc: string;
}

/**
 * Movement discriminator mirroring the backend StockMovementType — the four kinds of movement the
 * ledger records (entry, consumption, transfer, disposal).
 */
export type StockMovementType = 'Received' | 'Consumed' | 'Transferred' | 'Disposed';

/** A movement row of a single item's ledger (GET /stock-items/{id}/movements). */
export interface StockMovementListItem {
  id: string;
  stockItemId: string;
  type: StockMovementType;
  quantity: number;
  unit: string;
  /** Business date the movement occurred on, ISO "YYYY-MM-DD". */
  occurredAt: string;
  notes: string | null;
  /** Operator (responsável); null while the Inventory module has no user identity. */
  performedBy: string | null;
}

/** Filters applied to a stock item's movement ledger; empty values mean "no filter". */
export interface StockMovementsFilter {
  type?: StockMovementType;
  from?: string;
  to?: string;
}

/**
 * A cross-item recent-movement row (GET /api/inventory/stock-movements/recent). Unlike
 * {@link StockMovementListItem} (a single item's ledger), this carries the item's name so the
 * "recent activity" panel can list movements across every item without a second lookup.
 */
export interface RecentMovementItem {
  id: string;
  stockItemId: string;
  stockItemName: string;
  type: StockMovementType;
  quantity: number;
  unit: string;
  /** Business date the movement occurred on, ISO "YYYY-MM-DD"; null when the ledger has none. */
  occurredOn: string | null;
  /** Free-text note; null while the read model has no notes column. */
  notes: string | null;
  /**
   * Valued cost of the movement in BRL (quantity × the batch's unit cost), or null when it has no cost
   * (entries/transfers without a price, or unpriced draws). Cost is gestão-sensitive (card #110): the UI
   * only shows it to users holding Inventory.Cost.Read.
   */
  estimatedCostBrl: number | null;
}

/**
 * One month of consumption cost (GET /api/inventory/reports/cost-by-month) — card [E4] #109. Mirrors the
 * backend MonthlyCostItem: {@link month} is the first day of the month ("YYYY-MM-DD"); {@link totalCost}
 * is the summed BRL cost of the priced consumptions in it. Cost is gestão-sensitive (Inventory.Cost.Read).
 */
export interface MonthlyCostItem {
  /** First day of the month, ISO "YYYY-MM-DD". */
  month: string;
  totalCost: number;
}

/**
 * One experiment's consumption cost (GET /api/inventory/reports/cost-by-experiment) — card [E4] #109.
 * Mirrors the backend ExperimentCostItem: {@link experimentId} is null for the aggregated "no experiment"
 * bucket; {@link totalCost} is the summed BRL cost of its priced consumptions.
 */
export interface ExperimentCostItem {
  /** Cross-module experiment reference (by value); null folds the "no experiment" consumptions. */
  experimentId: string | null;
  totalCost: number;
}
