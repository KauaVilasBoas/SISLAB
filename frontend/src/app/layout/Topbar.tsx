import { Bell, UserCircle2 } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';

/**
 * Top bar with contextual actions. Slots for notifications and the account menu
 * (wired once auth lands).
 */
export function Topbar() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b bg-background px-4 md:px-6">
      <div className="text-sm text-muted-foreground">
        Sistema de Gestão de Laboratório
      </div>
      <div className="flex items-center gap-1">
        <Button variant="ghost" size="icon" aria-label="Notificações">
          <Bell className="size-5" />
        </Button>
        <Button variant="ghost" size="icon" aria-label="Conta">
          <UserCircle2 className="size-5" />
        </Button>
      </div>
    </header>
  );
}
