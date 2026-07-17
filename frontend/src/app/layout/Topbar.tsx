import { useLocation } from 'react-router-dom';
import { Search } from 'lucide-react';
import { CompanySwitcher } from '@/modules/auth/components/CompanySwitcher';
import { ThemeToggle } from '@/app/theme/ThemeToggle';
import { navItems } from '@/app/navigation';
import { NotificationsBell } from '@/modules/notifications/components/NotificationsBell';

/** Resolves the current screen's title/subtitle from the nav config (longest matching path). */
function useScreenHeading(): { title: string; subtitle: string } {
  const { pathname } = useLocation();

  const match =
    navItems.find((item) => item.path === pathname) ??
    navItems
      .filter((item) => item.path !== '/' && pathname.startsWith(item.path))
      .sort((a, b) => b.path.length - a.path.length)[0];

  return {
    title: match?.label ?? 'SISLAB',
    subtitle: match?.description ?? 'Sistema de Gestão de Laboratório',
  };
}

/**
 * Top bar for the authenticated shell (card [E7] #43). Left: the current screen title + subtitle.
 * Center: a global search placeholder (⌘K). Right: the active-company switcher, the notification center
 * (bell + dropdown, card #65), and the light/dark theme toggle. The signed-in user lives in the sidebar footer.
 */
export function Topbar() {
  const { title, subtitle } = useScreenHeading();

  return (
    <header className="flex h-14 shrink-0 items-center gap-4 border-b bg-background px-4 md:px-6">
      <div className="min-w-0 leading-tight">
        <h1 className="truncate text-sm font-semibold">{title}</h1>
        <p className="truncate text-xs text-muted-foreground">{subtitle}</p>
      </div>

      {/* Global search (placeholder — command palette is a later card). */}
      <button
        type="button"
        className="ml-auto hidden h-9 w-64 items-center gap-2 rounded-md border border-input bg-background px-3 text-sm text-muted-foreground transition-colors hover:bg-accent lg:flex"
        aria-label="Buscar"
      >
        <Search className="size-4" />
        <span className="flex-1 text-left">Buscar…</span>
        <kbd className="pointer-events-none rounded border bg-muted px-1.5 font-mono text-[10px] font-medium">
          ⌘K
        </kbd>
      </button>

      <div className="flex items-center gap-1 lg:ml-0 ml-auto">
        <CompanySwitcher />
        <NotificationsBell />
        <ThemeToggle />
      </div>
    </header>
  );
}
