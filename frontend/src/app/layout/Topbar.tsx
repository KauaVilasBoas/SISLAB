import { Bell } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { CompanySwitcher } from '@/modules/auth/components/CompanySwitcher';
import { UserMenu } from '@/modules/auth/components/UserMenu';

/**
 * Top bar for the authenticated shell (card [E7] #44). Shows the active-company switcher, the
 * notifications bell and the real signed-in user with a logout action.
 */
export function Topbar() {
  return (
    <header className="flex h-14 shrink-0 items-center justify-between border-b bg-background px-4 md:px-6">
      <CompanySwitcher />
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon" aria-label="Notificações">
          <Bell className="size-5" />
        </Button>
        <UserMenu />
      </div>
    </header>
  );
}
