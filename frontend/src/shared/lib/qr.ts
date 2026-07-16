/**
 * SISLAB QR payload grammar — the single source of truth for encoding and decoding the codes glued to
 * the laboratory's cabinets and containers. It is shared on purpose: the labels module (card [E7] #92)
 * *encodes* these payloads onto printable stickers, and the quick-consumption flow (card [E7] #63)
 * *decodes* them off the camera — both sides must agree on the exact same grammar, so it lives here in
 * `shared` rather than inside either feature module.
 *
 * Grammar:
 *   - `sislab:item:<guid>`     — a single stock item (its QR opens the quick baixa directly).
 *   - `sislab:location:<guid>` — a storage location / cabinet (its QR opens the location's item list so
 *                                the operator picks which item to draw from).
 *
 * The `sislab:` scheme namespaces SISLAB codes so an operator's camera cannot confuse an unrelated QR
 * (a URL, a boleto…) with ours; the middle segment (`item:` / `location:`) is the discriminator and
 * leaves room for future kinds without breaking this parser. The GUID is matched case-insensitively in
 * the canonical 8-4-4-4-12 hyphenated form and normalized to lower case so it matches the backend.
 */

/** The two kinds of SISLAB QR a label can carry / a scan can resolve to. */
export type ScannedQrKind = 'item' | 'location';

/** A successfully decoded SISLAB QR: which kind it is and the (lower-cased) aggregate id it points at. */
export interface ScannedQr {
  kind: ScannedQrKind;
  id: string;
}

const GUID_SOURCE = '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}';
const GUID_PATTERN = new RegExp(`^${GUID_SOURCE}$`, 'i');
const ITEM_QR_PATTERN = new RegExp(`^sislab:item:(${GUID_SOURCE})$`, 'i');
const LOCATION_QR_PATTERN = new RegExp(`^sislab:location:(${GUID_SOURCE})$`, 'i');

/** Builds the `sislab:item:<guid>` payload printed on a stock-item label. */
export function encodeStockItemQr(stockItemId: string): string {
  return `sislab:item:${stockItemId.trim().toLowerCase()}`;
}

/** Builds the `sislab:location:<guid>` payload printed on a storage-location (cabinet) label. */
export function encodeStorageLocationQr(storageLocationId: string): string {
  return `sislab:location:${storageLocationId.trim().toLowerCase()}`;
}

/**
 * Extracts the stock-item GUID from a `sislab:item:<guid>` payload, or `null` when the scanned text is
 * not a SISLAB item QR. Whitespace around the payload is tolerated (some encoders pad it); the id is
 * lower-cased so it matches the backend's canonical form.
 */
export function parseStockItemQr(payload: string): string | null {
  const match = ITEM_QR_PATTERN.exec(payload.trim());
  return match ? match[1].toLowerCase() : null;
}

/**
 * Extracts the storage-location GUID from a `sislab:location:<guid>` payload, or `null` when the scanned
 * text is not a SISLAB location QR. Same tolerance and normalization as {@link parseStockItemQr}.
 */
export function parseStorageLocationQr(payload: string): string | null {
  const match = LOCATION_QR_PATTERN.exec(payload.trim());
  return match ? match[1].toLowerCase() : null;
}

/**
 * Resolves any scanned/typed payload into a discriminated {@link ScannedQr}, trying the item form first
 * and then the location form. Returns `null` when neither shape matches, which the UI turns into a
 * friendly "QR não reconhecido" hint. This is the entry point the camera path uses, since a scan can be
 * either kind of label.
 */
export function resolveScannedQr(payload: string): ScannedQr | null {
  const itemId = parseStockItemQr(payload);
  if (itemId) return { kind: 'item', id: itemId };

  const locationId = parseStorageLocationQr(payload);
  if (locationId) return { kind: 'location', id: locationId };

  return null;
}

/**
 * Resolves a stock-item id from either a scanned `sislab:item:<guid>` QR or a bare GUID typed into the
 * keyboard fallback. The camera path always produces the namespaced form; the manual field is more
 * forgiving so an operator can paste just the id. Returns `null` when neither shape matches.
 */
export function resolveStockItemId(input: string): string | null {
  const fromQr = parseStockItemQr(input);
  if (fromQr) return fromQr;

  const trimmed = input.trim();
  return GUID_PATTERN.test(trimmed) ? trimmed.toLowerCase() : null;
}
