import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { authKeys, fetchMyPermissions } from '@/modules/auth/api/auth.queries';
import { useAuth } from '@/modules/auth/AuthProvider';

interface PermissionsContextValue {
  /** The effective permission codes of the signed-in user in the active company. */
  permissions: ReadonlySet<string>;
  /** True while the permission set is still loading (avoids flashing gated UI before it resolves). */
  isLoading: boolean;
  /**
   * True once the permission set is known (fetched or seeded from the auth bootstrap) — the safe moment
   * to render permission-gated UI. Equivalent to `!isLoading`, exposed as intent-revealing sugar.
   */
  isReady: boolean;
  /** True when the user holds the given permission code in the active company. */
  hasPermission: (code: string) => boolean;
  /** True when the user holds AT LEAST ONE of the given permission codes (empty list ⇒ false). */
  hasAnyPermission: (codes: readonly string[]) => boolean;
}

const PermissionsContext = createContext<PermissionsContextValue | null>(null);

/**
 * App-wide permission context (card [E7] #110). Sits ON TOP of {@link AuthProvider}: it reads "who is signed
 * in" and "which company is active" from the auth context and fetches the user's EFFECTIVE permission codes
 * for that company from GET /api/me/permissions — the single source the client-side permission gate reads.
 *
 * The permission set is company-scoped: a user may be gestão in one company and operador in another, so the
 * query is keyed by the active company and only runs once a company is active. Switching company clears the
 * cache (AuthProvider.selectCompany) and re-keys this query, so the gate always reflects the active tenant.
 *
 * No flash of gated UI: the AuthProvider bootstrap (and login) prime this exact query key in the React Query
 * cache BEFORE flipping status to 'authenticated', so when the AppShell first renders the set is already
 * resolved (isReady === true) — no second visible round-trip, no button that flickers on then off.
 *
 * The server remains the sole authority — every mutation/read is still enforced by [RequirePermission]. This
 * gate is UX only: it decides which permission-gated controls to render, never a security boundary.
 */
export function PermissionsProvider({ children }: { children: ReactNode }) {
  const { status, activeCompanyId } = useAuth();

  const query = useQuery({
    queryKey: authKeys.permissions(activeCompanyId),
    queryFn: fetchMyPermissions,
    // Only meaningful once authenticated with an active company — the endpoint returns [] without one anyway.
    enabled: status === 'authenticated' && Boolean(activeCompanyId),
    staleTime: 5 * 60_000,
  });

  const value = useMemo<PermissionsContextValue>(() => {
    const permissions = new Set(query.data?.permissions ?? []);
    // Loading only matters once the query is actually enabled; before a company is active there is nothing
    // to wait for, so gated UI simply renders as "no permission" rather than hanging on a spinner.
    const isLoading = query.isLoading && query.fetchStatus !== 'idle';
    return {
      permissions,
      isLoading,
      isReady: !isLoading,
      hasPermission: (code: string) => permissions.has(code),
      hasAnyPermission: (codes: readonly string[]) => codes.some((code) => permissions.has(code)),
    };
  }, [query.data, query.isLoading, query.fetchStatus]);

  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>;
}

/** Access the permission context. Throws when used outside <PermissionsProvider>. */
export function usePermissions(): PermissionsContextValue {
  const ctx = useContext(PermissionsContext);
  if (!ctx) throw new Error('usePermissions deve ser usado dentro de <PermissionsProvider>.');
  return ctx;
}

/** Sugar hook: does the signed-in user hold {@link code} in the active company? */
export function useHasPermission(code: string): boolean {
  return usePermissions().hasPermission(code);
}

interface RequirePermissionProps {
  /** The permission code the subtree requires (e.g. "Inventory.Cost.Read"). */
  code: string;
  children: ReactNode;
  /** Optional fallback rendered when the permission is absent (defaults to nothing). */
  fallback?: ReactNode;
}

/**
 * Conditional gate: renders {@link RequirePermissionProps.children} only when the signed-in user holds
 * {@link RequirePermissionProps.code} in the active company, otherwise the optional fallback. While the
 * permission set is still loading nothing is rendered, so a gated control never flashes before the gate
 * resolves. UX-only — the backend still enforces the permission on every call.
 */
export function RequirePermission({ code, children, fallback = null }: RequirePermissionProps) {
  const { hasPermission, isReady } = usePermissions();
  if (!isReady) return <>{fallback}</>;
  return <>{hasPermission(code) ? children : fallback}</>;
}

interface RequireAnyPermissionProps {
  /** The subtree renders when the user holds AT LEAST ONE of these codes. */
  codes: readonly string[];
  children: ReactNode;
  fallback?: ReactNode;
}

/**
 * Like {@link RequirePermission} but for a screen reachable through several capabilities (e.g. the
 * Members &amp; Profiles screen, visible to whoever can list members OR profiles). Renders the children
 * when the user holds any of {@link RequireAnyPermissionProps.codes}, otherwise the fallback; nothing
 * while the set is still resolving.
 */
export function RequireAnyPermission({ codes, children, fallback = null }: RequireAnyPermissionProps) {
  const { hasAnyPermission, isReady } = usePermissions();
  if (!isReady) return <>{fallback}</>;
  return <>{hasAnyPermission(codes) ? children : fallback}</>;
}
