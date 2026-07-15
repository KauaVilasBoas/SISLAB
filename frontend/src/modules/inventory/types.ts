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

/** Request body for an incoming stock entry (POST /stock-items/{id}/entries). */
export interface RegisterStockEntryRequest {
  quantity: number;
  unit: string;
  lotCode: string | null;
  expiryYear: number | null;
  expiryMonth: number | null;
  supplierPartnerId: string | null;
  occurredOn: string | null;
}

/** Request body for a consumption (POST /stock-items/{id}/consumptions). */
export interface RegisterConsumptionRequest {
  quantity: number;
  unit: string;
  experimentId: string | null;
  occurredOn: string | null;
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
