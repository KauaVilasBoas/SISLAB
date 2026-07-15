import {
  LayoutDashboard,
  Boxes,
  ShieldCheck,
  Monitor,
  Handshake,
  Users,
  Settings2,
  ScrollText,
  CalendarDays,
  Repeat,
  Presentation,
  FlaskConical,
  FlaskRound,
  type LucideIcon,
} from 'lucide-react';

export interface NavItem {
  /** Route path (also the React Router path). */
  path: string;
  label: string;
  /** Short description shown under the label in the sidebar and as the Topbar subtitle. */
  description: string;
  icon: LucideIcon;
  /** Future module without a card yet — rendered but non-navigable (card [E7] #43 scope note). */
  disabled?: boolean;
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}

/**
 * Grouped primary navigation matching the prototype shell (card [E7] #43).
 *
 * Only "Geral" (Dashboard) and "Recursos" are active today. "Agenda" and "Experimentos" are future
 * modules with no cards yet: they appear (so the structure is visible) but are disabled until
 * prioritized. "Administração" groups the already-built admin screens. Notifications live in the
 * Topbar bell, not here.
 */
export const navGroups: NavGroup[] = [
  {
    title: 'Geral',
    items: [
      {
        path: '/',
        label: 'Dashboard',
        description: 'Visão geral',
        icon: LayoutDashboard,
      },
    ],
  },
  {
    title: 'Agenda',
    items: [
      {
        path: '/agenda/scheduling',
        label: 'Agendamento',
        description: 'Em breve',
        icon: CalendarDays,
        disabled: true,
      },
      {
        path: '/agenda/vivarium',
        label: 'Troca do biotério',
        description: 'Em breve',
        icon: Repeat,
        disabled: true,
      },
      {
        path: '/agenda/talks',
        label: 'Apresentações',
        description: 'Em breve',
        icon: Presentation,
        disabled: true,
      },
    ],
  },
  {
    title: 'Experimentos',
    items: [
      {
        path: '/experiments/in-vitro',
        label: 'In vitro',
        description: 'Em breve',
        icon: FlaskRound,
        disabled: true,
      },
      {
        path: '/experiments/in-vivo',
        label: 'In vivo',
        description: 'Em breve',
        icon: FlaskConical,
        disabled: true,
      },
    ],
  },
  {
    title: 'Recursos',
    items: [
      {
        path: '/inventory',
        label: 'Estoque',
        description: 'Itens e movimentações',
        icon: Boxes,
      },
      {
        path: '/controlled',
        label: 'Controlados',
        description: 'Saldo por frasco',
        icon: ShieldCheck,
      },
      {
        path: '/equipment',
        label: 'Equipamentos',
        description: 'Calibração e manutenção',
        icon: Monitor,
      },
      {
        path: '/partners',
        label: 'Parceiros',
        description: 'Instituições e amostras',
        icon: Handshake,
      },
    ],
  },
  {
    title: 'Administração',
    items: [
      {
        path: '/members',
        label: 'Membros & Perfis',
        description: 'Acesso e permissões',
        icon: Users,
      },
      {
        path: '/configuration',
        label: 'Configurações',
        description: 'Catálogos do laboratório',
        icon: Settings2,
      },
      {
        path: '/audit',
        label: 'Auditoria',
        description: 'Trilha de ações',
        icon: ScrollText,
      },
    ],
  },
];

/** Flat list of navigable items — used to resolve the Topbar title/subtitle from the route. */
export const navItems: NavItem[] = navGroups.flatMap((group) => group.items);
