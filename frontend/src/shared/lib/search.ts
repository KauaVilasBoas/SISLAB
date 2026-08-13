/**
 * Client-side text matching helpers for small, in-memory lists (nav search, local filters).
 *
 * Lab users type without accents ("diluicao", "etiquetas") and in whatever case is handy, so every
 * comparison runs on a folded form instead of the raw string.
 */

/** Unicode combining marks — what NFD peels off an accented letter (e.g. "ç" → "c" + U+0327). */
const COMBINING_MARKS = /[\u0300-\u036f]/g;

/** Folds a string for comparison: lowercase, diacritics stripped and whitespace collapsed. */
export function normalizeForSearch(value: string): string {
  return value
    .normalize('NFD')
    .replace(COMBINING_MARKS, '')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}

/** Splits a user-typed query into folded terms; a blank query yields no terms (i.e. matches everything). */
export function toSearchTerms(query: string): string[] {
  const normalized = normalizeForSearch(query);
  return normalized ? normalized.split(' ') : [];
}

/**
 * True when EVERY term appears somewhere in the haystack — so "qr etiqueta" finds "Etiquetas QR"
 * regardless of the order the user typed the words in.
 */
export function matchesAllTerms(haystack: string, terms: readonly string[]): boolean {
  if (terms.length === 0) return true;
  const normalized = normalizeForSearch(haystack);
  return terms.every((term) => normalized.includes(term));
}
