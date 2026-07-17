/**
 * Partner module read/write contracts (card [E7] #48).
 *
 * Mirrors the SISLAB Inventory backend read models (PartnerListItem, PartnerDetail) and the write
 * request bodies exposed by PartnersController. The PartnerType enum crosses the wire as its NAME
 * (the API serializes it with JsonStringEnumConverter), so it is modelled as a string literal union.
 * Kept flat and primitive — the UI never sees the Partner aggregate or its value objects.
 */

/** The role a partner plays for the lab, mirroring the backend PartnerType enum. */
export type PartnerType = 'Supplier' | 'Client' | 'Both';

/** A partner row as listed by the partners grid (GET /api/inventory/partners). */
export interface PartnerListItem {
  id: string;
  name: string;
  type: PartnerType;
  /** Registration document (CNPJ), or null. */
  cnpj: string | null;
  /** Contact e-mail, or null. */
  email: string | null;
  /** Free-text description of what the partner supplies/does, or null. */
  notes: string | null;
  isActive: boolean;
}

/** A single partner's detail (GET /api/inventory/partners/{id}). Same shape as the list row today. */
export type PartnerDetail = PartnerListItem;

/** Filters applied to the partners listing; empty values mean "no filter". */
export interface PartnerFilters {
  type?: PartnerType;
  search?: string;
}

/** Request body for registering a new partner (POST /api/inventory/partners). */
export interface RegisterPartnerRequest {
  name: string;
  type: PartnerType;
  document: string | null;
  contactEmail: string | null;
  description: string | null;
}

/** Request body for updating a partner's descriptive data (PUT /api/inventory/partners/{id}). */
export interface UpdatePartnerRequest {
  name: string;
  type: PartnerType;
  document: string | null;
  contactEmail: string | null;
  description: string | null;
}
