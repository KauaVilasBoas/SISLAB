import { useNavigate } from 'react-router-dom';
import { LogOut } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { useAuth } from '@/modules/auth/AuthProvider';

/** Two-letter initials from a display name or e-mail, for the avatar chip. */
function initialsOf(value: string): string {
  const cleaned = value.trim();
  if (!cleaned) return '?';
  const parts = cleaned.split(/[\s@.]+/).filter(Boolean);
  const chars = parts.length >= 2 ? parts[0][0] + parts[1][0] : cleaned.slice(0, 2);
  return chars.toUpperCase();
}

/**
 * Topbar account chip + logout (card [E7] #44). Shows the real signed-in user (initials + name)
 * and signs out via POST /api/auth/logout, which clears the httpOnly cookies; the AuthProvider then
 * resets local state and the user lands back on /login.
 */
export function UserMenu() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  if (!user) return null;

  const displayName = user.username || user.email;

  async function handleLogout() {
    await logout();
    navigate('/login', { replace: true });
  }

  return (
    <div className="flex items-center gap-3">
      <div className="flex items-center gap-2">
        <span
          className="flex size-8 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary"
          aria-hidden
        >
          {initialsOf(displayName)}
        </span>
        <div className="hidden leading-tight sm:block">
          <p className="text-sm font-medium">{displayName}</p>
          <p className="text-xs text-muted-foreground">{user.email}</p>
        </div>
      </div>
      <Button
        variant="ghost"
        size="icon"
        aria-label="Sair"
        title="Sair"
        onClick={() => void handleLogout()}
      >
        <LogOut className="size-5" />
      </Button>
    </div>
  );
}
