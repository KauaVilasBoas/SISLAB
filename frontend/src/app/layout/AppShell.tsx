import { Outlet, useLocation } from 'react-router-dom';
import { Sidebar } from './Sidebar';
import { Topbar } from './Topbar';

/**
 * Authenticated application chrome: sidebar + topbar + routed content outlet.
 * The content is keyed by pathname so it replays a short fade on every route change (card [E7] #43).
 */
export function AppShell() {
  const { pathname } = useLocation();

  return (
    <div className="flex h-full">
      <Sidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />
        <main className="flex-1 overflow-auto bg-muted/30">
          <div key={pathname} className="page-enter p-5 md:p-6">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
