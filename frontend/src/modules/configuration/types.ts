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

// ---------------------------------------------------------------------------
// Experimental models / induction protocols (SISLAB-04)
// ---------------------------------------------------------------------------

/**
 * Kind of a standard (default) group, as a stable string code mirroring the backend
 * `StandardGroupKind` enum. Only a `Dose` group carries a dose amount + unit.
 */
export type StandardGroupKind = 'Naive' | 'Control' | 'Dose';

/** A compact experimental-model row for the "Modelos experimentais" listing (GET list). */
export interface ExperimentalModelListItem {
  id: string;
  name: string;
  description: string | null;
  inductionAdministrations: number;
  referenceDayAfterInduction: number;
}

/** The induction protocol of an experimental model (administrations, spacing, reference day). */
export interface InductionProtocol {
  administrations: number;
  intervalDays: number;
  referenceDayAfterInduction: number;
}

/** One standard group of an experimental model (name, role and — for a dose arm — its dose). */
export interface StandardGroup {
  name: string;
  kind: StandardGroupKind;
  doseAmount: number | null;
  doseUnit: string | null;
}

/** The default dilution parameters (µL per gram of animal + default diluent). */
export interface DilutionDefaults {
  microlitresPerGram: number;
  defaultDiluent: string;
}

/** The full experimental-model payload (GET {id}) — the structured detail view. */
export interface ExperimentalModelView {
  id: string;
  name: string;
  description: string | null;
  induction: InductionProtocol;
  /** Default timepoint labels the model measures at (e.g. basal, pós-indução, 7/15/21/28 dias). */
  timepoints: string[];
  /** Applicable physiological/behavioural parameter codes (e.g. glicemia, rotarod, peso). */
  parameters: string[];
  groups: StandardGroup[];
  dilutionDefaults: DilutionDefaults;
}

/** Request body for creating an experimental model — mirrors CreateExperimentalModelCommand. */
export interface CreateExperimentalModelRequest {
  name: string;
  description: string | null;
  induction: InductionProtocol;
  timepoints: string[];
  parameters: string[];
  groups: StandardGroup[];
  dilutionDefaults: DilutionDefaults;
}
