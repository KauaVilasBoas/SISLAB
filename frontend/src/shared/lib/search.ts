const COMBINING_MARKS = /[\u0300-\u036f]/g;

export function normalizeForSearch(value: string): string {
  return value
    .normalize('NFD')
    .replace(COMBINING_MARKS, '')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}

export function toSearchTerms(query: string): string[] {
  const normalized = normalizeForSearch(query);
  return normalized ? normalized.split(' ') : [];
}

export function matchesAllTerms(haystack: string, terms: readonly string[]): boolean {
  if (terms.length === 0) return true;
  const normalized = normalizeForSearch(haystack);
  return terms.every((term) => normalized.includes(term));
}
