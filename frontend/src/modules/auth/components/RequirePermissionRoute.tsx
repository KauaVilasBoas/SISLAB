import { type ReactNode } from 'react';
import { Loader2 } from 'lucide-react';
import { usePermissions } from '@/modules/auth/PermissionsProvider';
import { NoPermissionState } from '@/shared/components/NoPermissionState';

interface RequirePermissionRouteProps {
  /** The route renders only if the user holds AT LEAST ONE of these codes in the active company. */
  codes: readonly string[];
  children: ReactNode;
}

/**
 * Route-level permission guard (card [E7] #110). Wraps a permission-gated screen so that reaching it
 * directly by URL — without the required capability — renders a "sem permissão" panel instead of a broken
 * page or a 403 loop. Complements the sidebar filter (which hides the link) for users who type the path or
 * follow a stale bookmark. UX/defense-in-depth only: the backend still enforces [RequirePermission].
 *
 * While the permission set is still resolving it shows a spinner rather than the "no permission" panel, so
 * an authorized user never sees a flash of the denied state (the AuthProvider bootstrap primes the set, so
 * in practice this is already resolved on first paint).
 */
export function RequirePermissionRoute({ codes, children }: RequirePermissionRouteProps) {
  const { hasAnyPermission, isReady } = usePermissions();

  if (!isReady) {
    return (
      <div className="flex h-full items-center justify-center py-16">
        <Loader2 className="size-6 animate-spin text-muted-foreground" aria-label="Carregando" />
      </div>
    );
  }

  return hasAnyPermission(codes) ? <>{children}</> : <NoPermissionState />;
}
