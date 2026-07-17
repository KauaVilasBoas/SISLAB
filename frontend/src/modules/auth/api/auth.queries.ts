import { useMutation, useQuery } from '@tanstack/react-query';
import { api } from '@/shared/api/http';
import { Endpoints } from '@/shared/api/endpoints';
import type {
  ActiveCompany,
  CompanyMembership,
  CurrentUser,
  CurrentUserPermissions,
  LoginRequest,
  LoginResult,
} from '@/modules/auth/types';

/**
 * Auth query keys, namespaced under 'auth' so the AuthProvider can invalidate/reset the
 * session cache (me, companies, active) on login/logout without touching module caches.
 */
export const authKeys = {
  all: ['auth'] as const,
  me: () => [...authKeys.all, 'me'] as const,
  companies: () => [...authKeys.all, 'companies'] as const,
  activeCompany: () => [...authKeys.all, 'active-company'] as const,
  // Keyed by the active company: the effective permissions change with the active tenant, so switching
  // company (which also clears the cache) refetches under the new scope rather than reusing a stale set.
  permissions: (companyId: string | null) =>
    [...authKeys.all, 'permissions', companyId] as const,
};

// ---------------------------------------------------------------------------
// Imperative helpers (used by the AuthProvider bootstrap, which needs to await
// results and branch — not render-driven queries).
// ---------------------------------------------------------------------------

/** Arms the double-submit CSRF cookie. Called once on app bootstrap and after login. */
export function armCsrf(): Promise<void> {
  return api.get<void>(Endpoints.identity.auth.csrf);
}

/** Fetches the authenticated user. Rejects with ApiError(401) when there is no session. */
export function fetchCurrentUser(): Promise<CurrentUser> {
  return api.get<CurrentUser>(Endpoints.identity.auth.me);
}

/** Lists the companies the user belongs to (for auto-select / picker). */
export function fetchMyCompanies(): Promise<CompanyMembership[]> {
  return api.get<CompanyMembership[]>(Endpoints.identity.activeCompany.mine);
}

/** Resolves the active company from the httpOnly cookie; rejects with 404 when none is set. */
export function fetchActiveCompany(): Promise<ActiveCompany> {
  return api.get<ActiveCompany>(Endpoints.identity.activeCompany.active);
}

/** Fetches the signed-in user's effective permission codes in the active company (front permission gate). */
export function fetchMyPermissions(): Promise<CurrentUserPermissions> {
  return api.get<CurrentUserPermissions>(Endpoints.identity.auth.myPermissions);
}

/** Selects/switches the active company (writes the httpOnly active-company cookie). */
export function activateCompany(companyId: string): Promise<void> {
  return api.post<void>(Endpoints.identity.activeCompany.activate(companyId));
}

/** POST credentials — the backend sets the httpOnly session cookies on success. */
export function login(body: LoginRequest): Promise<LoginResult> {
  return api.post<LoginResult>(Endpoints.identity.auth.login, body);
}

/** Revokes the session and clears the httpOnly cookies. */
export function logout(): Promise<void> {
  return api.post<void>(Endpoints.identity.auth.logout, {});
}

// ---------------------------------------------------------------------------
// Render-driven hooks (used by components e.g. the Topbar company switcher).
// ---------------------------------------------------------------------------

/** The user's companies — feeds the Topbar company switcher. Enabled only when authenticated. */
export function useMyCompanies(enabled: boolean) {
  return useQuery({
    queryKey: authKeys.companies(),
    queryFn: fetchMyCompanies,
    enabled,
    staleTime: 5 * 60_000,
  });
}

/** Mutation wrapper for switching the active company from the Topbar. */
export function useActivateCompany() {
  return useMutation({
    mutationFn: (companyId: string) => activateCompany(companyId),
  });
}
