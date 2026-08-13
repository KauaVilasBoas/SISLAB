import { useLocation } from 'react-router-dom';
import { CompanySwitcher } from '@/modules/auth/components/CompanySwitcher';
import { ThemeToggle } from '@/app/theme/ThemeToggle';
import { navItems } from '@/app/navigation';
import { NavSearch } from '@/app/layout/NavSearch';
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
 * Right: the navigation search (⌘K), the active-company switcher, the notification center (bell +
 * dropdown, card #65), and the light/dark theme toggle. The signed-in user lives in the sidebar footer.
 */
export function Topbar() {
  const { title, subtitle } = useScreenHeading();

  return (
    <header className="flex h-14 shrink-0 items-center gap-4 border-b bg-background px-4 md:px-6">
      <div className="min-w-0 leading-tight">
        <h1 className="truncate text-sm font-semibold">{title}</h1>
        <p className="truncate text-xs text-muted-foreground">{subtitle}</p>
      </div>

      <div className="ml-auto flex items-center gap-1">
        <NavSearch />
        <CompanySwitcher />
        <NotificationsBell />
        <ThemeToggle />
      </div>
    </header>
  );
}
