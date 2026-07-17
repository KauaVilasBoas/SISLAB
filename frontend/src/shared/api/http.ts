import axios, {
  AxiosError,
  AxiosHeaders,
  type AxiosInstance,
  type AxiosResponse,
  type InternalAxiosRequestConfig,
} from 'axios';
import type { ApiError, ApiResult } from '@/shared/types/api';

/**
 * Central Axios instance for the whole app.
 *
 * Auth model (card [E7] #44): the session lives in an httpOnly cookie set by the backend
 * (POST /api/auth/login). The browser attaches it automatically — there is NO Bearer header
 * and NO localStorage token here by design (XSS-hardened, product-owner decision).
 *
 * - `withCredentials: true` so the session + CSRF cookies ride every request (and cross-origin
 *   Set-Cookie responses are honored).
 * - Request interceptor: on state-changing methods, echoes the readable XSRF-TOKEN cookie in the
 *   X-XSRF-TOKEN header (double-submit CSRF the backend enforces).
 * - Response interceptor: on 401 it triggers the registered unauthorized handler (logout + redirect
 *   to /login) and normalizes every failure into a typed ApiError (envelope OR RFC7807 ProblemDetails).
 * - `unwrap` returns just `data` for enveloped responses, and passes raw bodies through unchanged
 *   (Lumen auth + active-company endpoints don't use the ApiResult envelope).
 */

// ---------------------------------------------------------------------------
// CSRF (double-submit cookie)
// ---------------------------------------------------------------------------
const XSRF_COOKIE_NAME = 'XSRF-TOKEN';
const XSRF_HEADER_NAME = 'X-XSRF-TOKEN';
const UNSAFE_METHODS = new Set(['post', 'put', 'patch', 'delete']);

function readCookie(name: string): string | null {
  const match = document.cookie.split('; ').find((row) => row.startsWith(`${name}=`));
  return match ? decodeURIComponent(match.slice(name.length + 1)) : null;
}

// ---------------------------------------------------------------------------
// Unauthorized (401) handler — registered by the AuthProvider so this module
// stays free of React/router imports. Defaults to a hard redirect as a fallback.
// ---------------------------------------------------------------------------
type UnauthorizedHandler = () => void;

let onUnauthorized: UnauthorizedHandler = () => {
  if (window.location.pathname !== '/login') {
    window.location.assign('/login');
  }
};

/** Lets the AuthProvider replace the default 401 behavior with a router-aware logout. */
export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler;
}

export const httpClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
});

httpClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const method = (config.method ?? 'get').toLowerCase();
  if (UNSAFE_METHODS.has(method)) {
    const token = readCookie(XSRF_COOKIE_NAME);
    if (token) {
      const headers = AxiosHeaders.from(config.headers);
      headers.set(XSRF_HEADER_NAME, token);
      config.headers = headers;
    }
  }
  return config;
});

/**
 * Normalizes an Axios failure into the app's ApiError, reading whichever error contract the
 * backend returned:
 *  - the SISLAB envelope `{ success:false, message, errors }`, or
 *  - RFC7807 ProblemDetails `{ title, detail, errors }` (framework/domain errors, card #59).
 */
function toApiError(error: AxiosError<unknown>): ApiError {
  const status = error.response?.status ?? 0;
  const body = error.response?.data as
    | (Partial<ApiResult<unknown>> & {
        title?: string;
        detail?: string;
        errors?: Record<string, string[]>;
      })
    | undefined;

  const message =
    body?.message ||
    body?.detail ||
    body?.title ||
    error.message ||
    'Não foi possível completar a solicitação.';

  return { status, message, errors: body?.errors };
}

/** Marks a response body as the SISLAB envelope so `unwrap` can decide to peel `data`. */
function isEnvelope<T>(body: unknown): body is ApiResult<T> {
  return typeof body === 'object' && body !== null && 'success' in body && 'data' in body;
}

httpClient.interceptors.response.use(
  (response: AxiosResponse) => response,
  (error: AxiosError<unknown>) => {
    if (error.response?.status === 401) {
      onUnauthorized();
    }
    return Promise.reject(toApiError(error));
  },
);

/**
 * Returns the caller's payload. Enveloped responses are unwrapped to `data`; raw bodies
 * (Lumen auth, active-company endpoints) pass through as-is. A `success:false` envelope throws.
 */
function unwrap<T>(response: AxiosResponse<unknown>): T {
  const body = response.data;

  if (isEnvelope<T>(body)) {
    if (body.success === false) {
      throw {
        status: response.status,
        message: body.message ?? 'Operação não bem-sucedida.',
      } satisfies ApiError;
    }
    return body.data;
  }

  return body as T;
}

/** Thin typed helpers used by module `api/` layers. */
export const api = {
  async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    return unwrap<T>(await httpClient.get(url, { params }));
  },
  async post<T>(url: string, body?: unknown): Promise<T> {
    return unwrap<T>(await httpClient.post(url, body));
  },
  async put<T>(url: string, body?: unknown): Promise<T> {
    return unwrap<T>(await httpClient.put(url, body));
  },
  async patch<T>(url: string, body?: unknown): Promise<T> {
    return unwrap<T>(await httpClient.patch(url, body));
  },
  async del<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    return unwrap<T>(await httpClient.delete(url, { params }));
  },
};
