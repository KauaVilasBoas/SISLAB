/**
 * Controlled-substances module read contracts (card [E7] #62).
 *
 * The Controlados screen is a compliance view built on top of models the backend already exposes:
 * it lists controlled StockItems (the inventory read model narrowed to `is_controlled = true`) and
 * their append-only audit trail (the Audit module, card #57). Both shapes are re-declared here as the
 * module's own flat types so the compliance screen never reaches into the Inventory or Audit modules.
 */

import type { ExpiryStatus, StockItemListItem } from '@/modules/inventory/types';

/**
 * A controlled stock item as listed on the Controlados table. It is the inventory `StockItemListItem`
 * narrowed to what the compliance table renders (drug, lot, per-bottle balance, validity, container
 * state); reusing the type keeps the read model single-sourced.
 */
export type ControlledItem = StockItemListItem;

export type { ExpiryStatus };

/** Filters applied to the controlled listing; empty values mean "no filter". */
export interface ControlledFilters {
  /** Free-text search matched (ILIKE) server-side against name, lot code and brand. */
  search?: string;
}

/**
 * A single append-only audit-trail row, as returned by GET /api/audit/entries. `payload` is the raw
 * JSON string the writer stamped; the presentation layer parses it into the compliance-relevant fields
 * (counted quantity, divergence, reason) per action.
 */
export interface AuditTrailEntry {
  id: string;
  userId: string;
  action: string;
  entityType: string;
  entityId: string;
  payload: string;
  occurredAtUtc: string;
}

/**
 * Request body for registering a physical count (conference) of a controlled item
 * (POST /stock-items/{id}/counts). The operator (responsável) is the authenticated user, never sent.
 */
export interface RegisterStockCountRequest {
  countedQuantity: number;
  unit: string;
  occurredOn: string | null;
}

/**
 * The canonical Inventory audit actions the compliance trail renders (mirrors the backend
 * InventoryAuditActions vocabulary). Kept as a union so the presentation switch is exhaustive.
 */
export type AuditAction = 'consumption' | 'disposal' | 'stock-count';
