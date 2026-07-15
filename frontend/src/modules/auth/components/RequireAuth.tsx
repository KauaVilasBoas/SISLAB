import { type ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { useAuth } from '@/modules/auth/AuthProvider';

/**
 * Route guard for the authenticated AppShell (card [E7] #44).
 *
 * - While the session is being resolved (bootstrap GET /api/me) it shows a centered spinner so
 *   private routes never flash before we know who is signed in.
 * - When unauthenticated it redirects to /login, stashing the attempted location in router state
 *   so the LoginPage can send the user back after a successful login.
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  const location = useLocation();

  if (status === 'loading') {
    return (
      <div className="flex h-full items-center justify-center">
        <Loader2
          className="size-6 animate-spin text-muted-foreground"
          aria-label="Carregando"
        />
      </div>
    );
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <>{children}</>;
}
