/**
 * Configuration module read/write contracts (card [E12] #76).
 *
 * Mirrors the SISLAB Configuration backend DTOs (Application list queries + controller request
 * bodies). Kept flat and primitive — the UI never sees aggregate or Dapper shapes. Every catalogue
 * is tenant-scoped on the backend (active company from the httpOnly cookie), so no companyId here.
 */

// ---------------------------------------------------------------------------
// Units of measure
// ---------------------------------------------------------------------------

/** A unit of measure/consumption as listed on the "Units" tab. */
export interface UnitListItem {
  id: string;
  symbol: string;
  name: string;
}

/** Request body for creating a unit. */
export interface CreateUnitRequest {
  symbol: string;
  name: string;
}

// ---------------------------------------------------------------------------
// Rooms
// ---------------------------------------------------------------------------

/** A storage/procedure room as listed on the "Rooms" tab. */
export interface RoomListItem {
  id: string;
  name: string;
  requiresAuthorization: boolean;
}

/** Request body for creating a room. */
export interface CreateRoomRequest {
  name: string;
  requiresAuthorization: boolean;
}

// ---------------------------------------------------------------------------
// Item categories
// ---------------------------------------------------------------------------

/** A per-tenant item category as listed on the "Item categories" tab. */
export interface ItemCategoryListItem {
  id: string;
  name: string;
  /** Aliases already joined into a single, comma-separated string by the read-side. */
  aliases: string;
  isControlled: boolean;
}

/** Request body for creating an item category. */
export interface CreateItemCategoryRequest {
  name: string;
  aliases: string[] | null;
  isControlled: boolean;
}

// ---------------------------------------------------------------------------
// Reference ranges
// ---------------------------------------------------------------------------

/** A healthy analyte interval as listed on the "Reference ranges" tab. */
export interface ReferenceRangeListItem {
  id: string;
  analyte: string;
  species: string;
  minimum: number | null;
  maximum: number | null;
  unit: string | null;
}

/** Request body for creating a reference range. */
export interface CreateReferenceRangeRequest {
  analyte: string;
  species: string;
  minimum: number | null;
  maximum: number | null;
  unit: string | null;
}

// ---------------------------------------------------------------------------
// Expiry policy
// ---------------------------------------------------------------------------

/** Request body for setting the expiry warning window (days before expiry). */
export interface SetExpiryWarningWindowRequest {
  warningWindowDays: number;
}
