import { Moon, Sun } from 'lucide-react';
import { Button } from '@/shared/components/ui/button';
import { useTheme } from '@/app/theme/ThemeProvider';

/** Sun/moon theme switch for the Topbar (card [E7] #43). */
export function ThemeToggle() {
  const { theme, toggleTheme } = useTheme();
  const isDark = theme === 'dark';

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={toggleTheme}
      aria-label={isDark ? 'Ativar tema claro' : 'Ativar tema escuro'}
      title={isDark ? 'Tema claro' : 'Tema escuro'}
    >
      {isDark ? <Sun className="size-5" /> : <Moon className="size-5" />}
    </Button>
  );
}
