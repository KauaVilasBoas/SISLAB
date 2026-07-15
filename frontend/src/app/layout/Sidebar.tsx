import { NavLink, useNavigate } from 'react-router-dom';
import { FlaskConical, QrCode, LogOut } from 'lucide-react';
import { navGroups, type NavItem } from '@/app/navigation';
import { useAuth } from '@/modules/auth/AuthProvider';
import { cn } from '@/shared/lib/utils';

/** Two-letter initials from a display name or e-mail, for the avatar chip. */
function initialsOf(value: string): string {
  const cleaned = value.trim();
  if (!cleaned) return '?';
  const parts = cleaned.split(/[\s@.]+/).filter(Boolean);
  const chars = parts.length >= 2 ? parts[0][0] + parts[1][0] : cleaned.slice(0, 2);
  return chars.toUpperCase();
}

/**
 * Fixed left navigation (card [E7] #43). Always-dark 256px surface (prototype #0b1220) with the
 * SISLAB brand, a "Registro rápido" quick action, grouped nav (future groups shown but disabled),
 * and a footer with the signed-in user + logout.
 */
export function Sidebar() {
  return (
    <aside className="hidden w-64 shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground md:flex">
      {/* Brand */}
      <div className="flex h-14 items-center gap-2 border-b border-sidebar-border px-5">
        <FlaskConical className="size-5 text-status-info" />
        <div className="leading-tight">
          <p className="text-sm font-semibold tracking-tight">SISLAB</p>
          <p className="text-[11px] text-sidebar-muted">LAFTE · UFBA</p>
        </div>
      </div>

      {/* Quick action */}
      <div className="px-3 pt-3">
        <button
          type="button"
          disabled
          title="Registro rápido (em breve — #63)"
          className="flex w-full items-center justify-center gap-2 rounded-md bg-status-info/15 px-3 py-2 text-sm font-medium text-sidebar-foreground ring-1 ring-inset ring-status-info/30 transition-colors hover:bg-status-info/25 disabled:cursor-not-allowed disabled:opacity-70"
        >
          <QrCode className="size-4" />
          Registro rápido
        </button>
      </div>

      {/* Grouped navigation */}
      <nav className="flex-1 space-y-5 overflow-y-auto p-3">
        {navGroups.map((group) => (
          <div key={group.title} className="space-y-1">
            <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wider text-sidebar-muted">
              {group.title}
            </p>
            {group.items.map((item) => (
              <SidebarItem key={item.path} item={item} />
            ))}
          </div>
        ))}
      </nav>

      <SidebarUserFooter />
    </aside>
  );
}

function SidebarItem({ item }: { item: NavItem }) {
  const Icon = item.icon;

  if (item.disabled) {
    return (
      <div
        aria-disabled
        title="Módulo futuro"
        className="flex cursor-not-allowed items-center gap-3 rounded-md px-3 py-2 opacity-45"
      >
        <Icon className="size-4 shrink-0" />
        <span className="min-w-0">
          <span className="block truncate text-sm font-medium">{item.label}</span>
          <span className="block truncate text-[11px] text-sidebar-muted">
            {item.description}
          </span>
        </span>
      </div>
    );
  }

  return (
    <NavLink
      to={item.path}
      end={item.path === '/'}
      className={({ isActive }) =>
        cn(
          'flex items-center gap-3 rounded-md px-3 py-2 transition-colors',
          isActive
            ? 'bg-sidebar-accent text-sidebar-accent-foreground'
            : 'text-sidebar-muted hover:bg-sidebar-accent/60 hover:text-sidebar-foreground',
        )
      }
    >
      <Icon className="size-4 shrink-0" />
      <span className="min-w-0">
        <span className="block truncate text-sm font-medium">{item.label}</span>
        <span className="block truncate text-[11px] opacity-80">{item.description}</span>
      </span>
    </NavLink>
  );
}

function SidebarUserFooter() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  if (!user) return null;

  const displayName = user.username || user.email;
  const role = user.profiles[0]?.name ?? 'Membro';

  async function handleLogout() {
    await logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="flex items-center gap-3 border-t border-sidebar-border p-3">
      <span
        className="flex size-9 shrink-0 items-center justify-center rounded-full bg-sidebar-accent text-xs font-semibold text-sidebar-accent-foreground"
        aria-hidden
      >
        {initialsOf(displayName)}
      </span>
      <div className="min-w-0 flex-1 leading-tight">
        <p className="truncate text-sm font-medium">{displayName}</p>
        <p className="truncate text-[11px] text-sidebar-muted">{role}</p>
      </div>
      <button
        type="button"
        onClick={() => void handleLogout()}
        aria-label="Sair"
        title="Sair"
        className="rounded-md p-2 text-sidebar-muted transition-colors hover:bg-sidebar-accent hover:text-sidebar-foreground"
      >
        <LogOut className="size-4" />
      </button>
    </div>
  );
}
