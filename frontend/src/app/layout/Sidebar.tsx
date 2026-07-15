import { NavLink } from 'react-router-dom';
import { FlaskConical } from 'lucide-react';
import { navItems } from '@/app/navigation';
import { cn } from '@/shared/lib/utils';

/**
 * Fixed left navigation. Highlights the active route via NavLink.
 */
export function Sidebar() {
  return (
    <aside className="hidden w-64 shrink-0 flex-col border-r bg-card md:flex">
      <div className="flex h-14 items-center gap-2 border-b px-6">
        <FlaskConical className="size-5 text-primary" />
        <span className="text-lg font-semibold tracking-tight">SISLAB</span>
      </div>
      <nav className="flex-1 space-y-1 p-3">
        {navItems.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            end={item.path === '/'}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                isActive
                  ? 'bg-accent text-accent-foreground'
                  : 'text-muted-foreground hover:bg-accent/60 hover:text-foreground',
              )
            }
          >
            <item.icon className="size-4" />
            {item.label}
          </NavLink>
        ))}
      </nav>
    </aside>
  );
}
