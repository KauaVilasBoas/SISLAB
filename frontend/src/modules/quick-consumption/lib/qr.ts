/**
 * QR payload grammar for the mobile quick-consumption flow (card [E7] #63).
 *
 * The label glued to a cabinet/item encodes a namespaced URI `sislab:item:<guid>`: the `sislab:`
 * scheme keeps SISLAB codes from being confused with arbitrary QR content the camera might see, and
 * the `item:` segment reserves room for future kinds (e.g. `sislab:location:<guid>` for cabinet QRs,
 * deferred to #92) without breaking this parser. Only the item form is understood today.
 *
 * The GUID is matched case-insensitively in the canonical 8-4-4-4-12 hyphenated form; a payload that
 * does not match yields `null`, which the UI turns into a friendly "QR não reconhecido" message.
 */

const ITEM_QR_PATTERN =
  /^sislab:item:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$/i;

/**
 * Extracts the stock-item GUID from a `sislab:item:<guid>` payload, or `null` when the scanned text
 * is not a SISLAB item QR. Whitespace around the payload is tolerated (some encoders pad it); the id
 * is lower-cased so it matches the backend's canonical form.
 */
export function parseStockItemQr(payload: string): string | null {
  const match = ITEM_QR_PATTERN.exec(payload.trim());
  return match ? match[1].toLowerCase() : null;
}

const GUID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

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
