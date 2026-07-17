import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import { authKeys, fetchMyPermissions } from '@/modules/auth/api/auth.queries';
import { useAuth } from '@/modules/auth/AuthProvider';

interface PermissionsContextValue {
  /** The effective permission codes of the signed-in user in the active company. */
  permissions: ReadonlySet<string>;
  /** True while the permission set is still loading (avoids flashing gated UI before it resolves). */
  isLoading: boolean;
  /** True when the user holds the given permission code in the active company. */
  hasPermission: (code: string) => boolean;
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
    return {
      permissions,
      isLoading: query.isLoading,
      hasPermission: (code: string) => permissions.has(code),
    };
  }, [query.data, query.isLoading]);

  return <PermissionsContext.Provider value={value}>{children}</PermissionsContext.Provider>;
}

/** Access the permission context. Throws when used outside <PermissionsProvider>. */
export function usePermissions(): PermissionsContextValue {
  const ctx = useContext(PermissionsContext);
  if (!ctx) throw new Error('usePermissions deve ser usado dentro de <PermissionsProvider>.');
  return ctx;
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
  const { hasPermission, isLoading } = usePermissions();
  if (isLoading) return <>{fallback}</>;
  return <>{hasPermission(code) ? children : fallback}</>;
}
