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
 * - baseURL empty in dev => requests hit /api and Vite proxies them to the backend
 *   (same-origin, no CORS). In prod set VITE_API_BASE_URL to the API host.
 * - Request interceptor attaches the bearer token (Lumen-issued) when present.
 * - Response interceptor unwraps the { success, message, data } envelope and
 *   normalizes failures into a typed ApiError.
 */
const TOKEN_STORAGE_KEY = 'sislab.accessToken';

export function getAccessToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
}

export function setAccessToken(token: string | null): void {
  if (token) localStorage.setItem(TOKEN_STORAGE_KEY, token);
  else localStorage.removeItem(TOKEN_STORAGE_KEY);
}

export const httpClient: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  headers: { 'Content-Type': 'application/json' },
});

httpClient.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = getAccessToken();
  if (token) {
    const headers = AxiosHeaders.from(config.headers);
    headers.set('Authorization', `Bearer ${token}`);
    config.headers = headers;
  }
  return config;
});

function toApiError(error: AxiosError<ApiResult<unknown>>): ApiError {
  const status = error.response?.status ?? 0;
  const envelope = error.response?.data;
  return {
    status,
    message:
      envelope?.message ||
      error.message ||
      'Não foi possível completar a solicitação.',
    errors: (envelope as { errors?: Record<string, string[]> })?.errors,
  };
}

httpClient.interceptors.response.use(
  (response: AxiosResponse<ApiResult<unknown>>) => response,
  (error: AxiosError<ApiResult<unknown>>) => Promise.reject(toApiError(error)),
);

/** Unwrap the envelope, returning just `data`. Throws ApiError on failure. */
function unwrap<T>(response: AxiosResponse<ApiResult<T>>): T {
  const envelope = response.data;
  if (envelope && envelope.success === false) {
    throw {
      status: response.status,
      message: envelope.message ?? 'Operação não bem-sucedida.',
    } satisfies ApiError;
  }
  return envelope.data;
}

/** Thin typed helpers used by module `api/` layers. */
export const api = {
  async get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    return unwrap(await httpClient.get<ApiResult<T>>(url, { params }));
  },
  async post<T>(url: string, body?: unknown): Promise<T> {
    return unwrap(await httpClient.post<ApiResult<T>>(url, body));
  },
  async put<T>(url: string, body?: unknown): Promise<T> {
    return unwrap(await httpClient.put<ApiResult<T>>(url, body));
  },
  async del<T>(url: string, params?: Record<string, unknown>): Promise<T> {
    return unwrap(await httpClient.delete<ApiResult<T>>(url, { params }));
  },
};
