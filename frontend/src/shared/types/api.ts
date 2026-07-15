/**
 * Mirrors the backend response contracts.
 *
 * Every SISLAB endpoint wraps its payload in an envelope:
 *   { success: boolean, message: string | null, data: T }
 * The Axios client (shared/api/http.ts) unwraps `data` for callers, but the raw
 * envelope type is kept here so error handling can read `success`/`message`.
 */
export interface ApiResult<T> {
  success: boolean;
  message: string | null;
  data: T;
}

/** Standard shape for paginated read-side queries. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Normalized error surfaced by the Axios interceptor. */
export interface ApiError {
  status: number;
  message: string;
  /** Field-level validation errors when the backend returns 422. */
  errors?: Record<string, string[]>;
}
