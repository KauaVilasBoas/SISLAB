import {
  LayoutDashboard,
  Boxes,
  Users,
  Settings2,
  ScrollText,
  Bell,
  type LucideIcon,
} from 'lucide-react';

export interface NavItem {
  /** Route path (also used as the React Router path). */
  path: string;
  label: string;
  icon: LucideIcon;
}

/**
 * Primary navigation, one entry per backend module. The Sidebar renders this and
 * the router (app/router.tsx) wires each `path` to its module's mother screen.
 */
export const navItems: NavItem[] = [
  { path: '/', label: 'Painel', icon: LayoutDashboard },
  { path: '/inventory', label: 'Estoque', icon: Boxes },
  { path: '/members', label: 'Membros & Perfis', icon: Users },
  { path: '/configuration', label: 'Configurações', icon: Settings2 },
  { path: '/audit', label: 'Auditoria', icon: ScrollText },
  { path: '/notifications', label: 'Notificações', icon: Bell },
];
