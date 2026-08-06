import { useEffect } from 'react';
import { isRouteErrorResponse, useNavigate, useRouteError } from 'react-router-dom';
import { AlertTriangle, RefreshCw, Home } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';

/**
 * The router's `errorElement` — what the user sees when a route crashes while rendering.
 *
 * Without it React Router falls back to its own development screen ("Unexpected Application Error!"
 * plus a raw minified stack trace), which is fine locally and indefensible on the public demo.
 *
 * A render crash here is nearly always recoverable by loading the app again: a stale asset chunk
 * after a deploy, or a request that resolved with an unexpected body (see the HTML guard in
 * shared/api/http.ts). So the boundary retries once, silently, and only shows itself if the crash
 * repeats — a second failure is a real bug, and reloading again would trap the user in a loop.
 */

const RELOAD_GUARD_KEY = 'sislab:last-error-reload';

/** How long before the boundary is willing to spend another automatic reload. */
const RELOAD_COOLDOWN_MS = 5 * 60_000;

/**
 * True at most once per cooldown window, and never when sessionStorage is unavailable — an
 * auto-reload we cannot rate-limit is an infinite refresh loop, which is worse than the error.
 */
function claimAutoReload(): boolean {
  try {
    const last = Number(window.sessionStorage.getItem(RELOAD_GUARD_KEY) ?? 0);
    if (Date.now() - last < RELOAD_COOLDOWN_MS) return false;
    window.sessionStorage.setItem(RELOAD_GUARD_KEY, String(Date.now()));
    return true;
  } catch {
    return false;
  }
}

/** Best-effort technical detail, shown only in development. */
function describe(error: unknown): string | null {
  if (isRouteErrorResponse(error)) return `${error.status} ${error.statusText}`;
  if (error instanceof Error) return error.stack ?? error.message;
  return typeof error === 'string' ? error : null;
}

export function RouteErrorBoundary() {
  const error = useRouteError();
  const navigate = useNavigate();
  const detail = describe(error);

  useEffect(() => {
    if (claimAutoReload()) window.location.reload();
  }, []);

  return (
    <div className="flex min-h-screen items-center justify-center bg-background p-6">
      <div className="w-full max-w-md rounded-lg border border-border bg-card p-8 text-center shadow-sm">
        <div className="mx-auto mb-5 flex size-12 items-center justify-center rounded-full bg-destructive/10">
          <AlertTriangle className="size-6 text-destructive" />
        </div>

        <h1 className="text-lg font-semibold text-foreground">
          Não foi possível carregar esta tela
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Algo inesperado aconteceu ao montar a página. Recarregar costuma resolver.
        </p>

        <div className="mt-6 flex flex-col gap-2 sm:flex-row sm:justify-center">
          <Button onClick={() => window.location.reload()}>
            <RefreshCw />
            Recarregar
          </Button>
          <Button variant="outline" onClick={() => navigate('/', { replace: true })}>
            <Home />
            Ir para o início
          </Button>
        </div>

        {import.meta.env.DEV && detail && (
          <pre className="mt-6 max-h-52 overflow-auto rounded-md bg-muted p-3 text-left text-xs text-muted-foreground">
            {detail}
          </pre>
        )}
      </div>
    </div>
  );
}
