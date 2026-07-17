import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { setUnauthorizedHandler } from '@/shared/api/http';
import type { ApiError } from '@/shared/types/api';
import {
  activateCompany as activateCompanyRequest,
  armCsrf,
  authKeys,
  fetchActiveCompany,
  fetchCurrentUser,
  fetchMyCompanies,
  fetchMyPermissions,
  login as loginRequest,
  logout as logoutRequest,
} from '@/modules/auth/api/auth.queries';
import type { CompanyMembership, CurrentUser } from '@/modules/auth/types';

type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated';

/**
 * Outcome of a login attempt, so the LoginPage can decide the next step:
 *  - 'active'      → a company is already active (auto-selected because the user has exactly one);
 *  - 'select'      → the user belongs to several companies and must pick one (`companies` provided);
 *  - 'no-company'  → the user has no company membership at all (edge case; surfaced to the user).
 */
export type LoginOutcome =
  | { kind: 'active' }
  | { kind: 'select'; companies: CompanyMembership[] }
  | { kind: 'no-company' };

interface AuthContextValue {
  status: AuthStatus;
  user: CurrentUser | null;
  /** Active company id resolved from the httpOnly cookie, or null when none is selected yet. */
  activeCompanyId: string | null;
  login: (identifier: string, password: string) => Promise<LoginOutcome>;
  logout: () => Promise<void>;
  /** Activates the chosen company (post-login picker or Topbar switcher) and updates state. */
  selectCompany: (companyId: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function isApiError(error: unknown): error is ApiError {
  return typeof error === 'object' && error !== null && 'status' in error;
}

/**
 * App-wide authentication context (card [E7] #44).
 *
 * Bootstrap: arm CSRF, then GET /api/me. A 401 means "not signed in" (no error surfaced).
 * When authenticated it also resolves the active company from the cookie (404 = none yet).
 *
 * The session itself lives entirely in httpOnly cookies managed by the backend — this provider
 * only mirrors "who is signed in" and "which company is active" into React state and drives the
 * login/logout/company-selection flows.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [activeCompanyId, setActiveCompanyId] = useState<string | null>(null);

  // Keeps the 401 handler stable while always seeing the latest reset logic.
  const resetSession = useCallback(() => {
    setUser(null);
    setActiveCompanyId(null);
    setStatus('unauthenticated');
    queryClient.removeQueries({ queryKey: authKeys.all });
  }, [queryClient]);

  const resetRef = useRef(resetSession);
  resetRef.current = resetSession;

  // Any 401 from the API (expired/absent session) drops us to unauthenticated; RequireAuth then
  // redirects to /login. Registered once — the handler reads the latest reset via the ref.
  useEffect(() => {
    setUnauthorizedHandler(() => resetRef.current());
  }, []);

  // Primes the permission-gate cache for the given company BEFORE the UI renders, so the AppShell's gated
  // controls resolve on their first paint (no flash-then-hide). Keyed identically to PermissionsProvider's
  // query, so that provider reuses this cached set instead of firing a second visible round-trip. A failure
  // here must never block bootstrap/login — the PermissionsProvider query will simply fetch on mount.
  const primePermissions = useCallback(
    async (companyId: string) => {
      try {
        await queryClient.prefetchQuery({
          queryKey: authKeys.permissions(companyId),
          queryFn: fetchMyPermissions,
          staleTime: 5 * 60_000,
        });
      } catch {
        // Non-fatal: gated UI defaults to hidden until the provider query resolves it.
      }
    },
    [queryClient],
  );

  const resolveActiveCompany = useCallback(async () => {
    try {
      const active = await fetchActiveCompany();
      // Prime the permission set for this company before flipping to 'authenticated', so gated UI never flashes.
      await primePermissions(active.companyId);
      setActiveCompanyId(active.companyId);
    } catch (error) {
      // 404 = no active company selected yet; leave null so the picker/guard can handle it.
      if (!isApiError(error) || error.status !== 404) throw error;
      setActiveCompanyId(null);
    }
  }, [primePermissions]);

  // Bootstrap on mount.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        await armCsrf();
      } catch {
        // A failed CSRF arm must not block bootstrap; unsafe requests will simply be re-armed later.
      }

      try {
        const me = await fetchCurrentUser();
        if (cancelled) return;
        setUser(me);
        await resolveActiveCompany();
        if (cancelled) return;
        setStatus('authenticated');
      } catch {
        // 401 (or any bootstrap failure) → treat as signed out.
        if (!cancelled) setStatus('unauthenticated');
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [resolveActiveCompany]);

  const login = useCallback(
    async (identifier: string, password: string): Promise<LoginOutcome> => {
      await loginRequest({ identifier, password });

      // Re-arm CSRF: the token is bound to the new session cookie the backend just set.
      await armCsrf();

      const me = await fetchCurrentUser();
      setUser(me);
      setStatus('authenticated');

      const companies = await fetchMyCompanies();

      if (companies.length === 0) {
        setActiveCompanyId(null);
        return { kind: 'no-company' };
      }

      if (companies.length === 1) {
        await activateCompanyRequest(companies[0].id);
        // Prime the gate before the AppShell mounts so no gated control flashes on the first authenticated paint.
        await primePermissions(companies[0].id);
        setActiveCompanyId(companies[0].id);
        return { kind: 'active' };
      }

      return { kind: 'select', companies };
    },
    [primePermissions],
  );

  const selectCompany = useCallback(
    async (companyId: string) => {
      await activateCompanyRequest(companyId);
      // The active tenant changed — drop tenant-scoped caches so screens refetch under the new company.
      queryClient.clear();
      // Re-prime the gate for the newly active company so the shell renders with its permissions resolved.
      await primePermissions(companyId);
      setActiveCompanyId(companyId);
    },
    [queryClient, primePermissions],
  );

  const logout = useCallback(async () => {
    try {
      await logoutRequest();
    } catch {
      // Even if the server call fails, clear local session — the cookies are httpOnly and the
      // backend also clears them on logout; a stale server session will 401 on the next call.
    } finally {
      resetSession();
    }
  }, [resetSession]);

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, activeCompanyId, login, logout, selectCompany }),
    [status, user, activeCompanyId, login, logout, selectCompany],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** Access the auth context. Throws when used outside <AuthProvider>. */
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>.');
  return ctx;
}
